using System.Security.Claims;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Catalog.Api.Stores;
using dCMS.Core.Commands;
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Core.Services;
using Microsoft.Extensions.Configuration;
using VariantAxisDef = dCMS.Core.Services.ProductVariantGeneratorService.VariantAxisDefinition;

namespace dCMS.Catalog.Api.Products;

public static class ProductRoutes
{
    public static void MapProductRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/products")
            .WithTags("catalog-products")
            .WithTenantStoreAccess(configuration);

        Auth(g.MapGet("", SearchProducts), auth, write: false);
        AuthApproval(g.MapGet("pending-approvals", ListPendingApprovals), auth);
        Auth(g.MapGet("{productId}/variants", ListVariants), auth, write: false);
        Auth(g.MapGet("{productId}/recommendations", ListRecommendations), auth, write: false);
        Auth(g.MapPut("{productId}/recommendations", SetRecommendations), auth, write: true);
        Auth(g.MapGet("{productId}/approval-comments", ListApprovalComments), auth, write: false);
        Auth(g.MapPost("{productId}/approval-comments", PostApprovalComment), auth, write: true);
        Auth(g.MapGet("{productId}", GetProduct), auth, write: false);
        Auth(g.MapPost("bulk", BulkPostProducts), auth, write: true);
        Auth(g.MapPut("bulk", BulkUpdateProducts), auth, write: true);
        Auth(g.MapPost("", CreateProduct), auth, write: true);
        Auth(g.MapPut("{productId}", UpdateProduct), auth, write: true);
        Auth(g.MapPatch("{productId}", UpdateProduct), auth, write: true);
        Auth(g.MapDelete("{productId}", DeleteProductAsArchive), auth, write: true);
        Auth(g.MapPost("{productId}/submit-for-approval", SubmitForApproval), auth, write: true);
        Auth(g.MapPost("{productId}/submit-for-archive", SubmitForArchive), auth, write: true);
        AuthApproval(g.MapPost("{productId}/approve", ApprovePending), auth);
        AuthApproval(g.MapPost("{productId}/request-changes", RequestChanges), auth);
        AuthApproval(g.MapPost("{productId}/reject", RejectPending), auth);
        Auth(g.MapPost("{productId}/publish", PublishProduct), auth, write: true);
        Auth(g.MapPost("{productId}/hide", HideProduct), auth, write: true);
        Auth(g.MapPost("{productId}/unhide", UnhideProduct), auth, write: true);
        Auth(g.MapPost("{productId}/archive", ArchiveProduct), auth, write: true);
        Auth(g.MapPost("{productId}/variants/generate", GenerateVariants), auth, write: true);
        Auth(g.MapPost("{productId}/variants", CreateManualVariant), auth, write: true);
        Auth(g.MapPut("{productId}/variants/{variantId}", UpdateVariant), auth, write: true);
    }

    private const int BulkMaxItems = 100;

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder builder, bool authEnabled, bool write) =>
        authEnabled
            ? builder.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : builder;

    private static RouteHandlerBuilder AuthApproval(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.CatalogApproval) : builder;

    private static string ActorUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

    private static string ActorCatalogRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(DcmsRoles.SuperAdmin)) return DcmsRoles.SuperAdmin;
        if (user.IsInRole(DcmsRoles.ChainAdmin)) return DcmsRoles.ChainAdmin;
        if (user.IsInRole(DcmsRoles.BrandManager)) return DcmsRoles.BrandManager;
        if (user.IsInRole(DcmsRoles.StoreManager)) return DcmsRoles.StoreManager;
        if (user.IsInRole(DcmsRoles.StoreStaff)) return DcmsRoles.StoreStaff;
        return "user";
    }

    private static bool CanBypassStoreApprovalGate(ClaimsPrincipal user) =>
        user.IsInRole(DcmsRoles.SuperAdmin) || user.IsInRole(DcmsRoles.ChainAdmin) ||
        user.IsInRole(DcmsRoles.BrandManager);

    /// <summary>Maps the backoffice eStore status filter to persisted status values. No filter → all non-archived.</summary>
    private static IReadOnlyList<string> MapAdminStatuses(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "live" or "active" => new[] { "active" },
            "draft" => new[] { "draft" },
            "pending" or "pending_approval" => new[] { "pending_approval" },
            "pending_archive" => new[] { "pending_archive" },
            "inactive" or "hidden" => new[] { "hidden" },
            "archived" => new[] { "archived" },
            _ => new[] { "draft", "pending_approval", "pending_archive", "active", "hidden" },
        };

    /// <summary>US-6/7: Elasticsearch search + filters/facets; optional Redis cache (30s) when <c>ConnectionStrings:Redis</c> is set.</summary>
    private static async Task<IResult> SearchProducts(
        HttpContext http,
        IConfiguration configuration,
        string tenantId,
        string storeId,
        IProductSearchQuery search,
        string? q,
        bool? inStock,
        int? category,
        int? pageSize,
        string? cursor,
        long? minPrice,
        long? maxPrice,
        string? sort,
        bool? facets,
        string? filters,
        string? status,
        string? brand,
        string? categories,
        string? quickAccess,
        CancellationToken cancellationToken)
    {
        var redisConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis"));
        http.Response.Headers.CacheControl = redisConfigured ? "private, max-age=30" : "no-store";

        if (!TryParseAttributeFilters(filters, out var attrFilters, out var filterError))
            return ApiEnvelope.Error("validation_error", filterError ?? "Invalid filters.", StatusCodes.Status400BadRequest);

        if (minPrice is < 0 || maxPrice is < 0 || (minPrice is not null && maxPrice is not null && minPrice > maxPrice))
            return ApiEnvelope.Error("validation_error", "Invalid price range.", StatusCodes.Status400BadRequest);

        var sortEnum = sort?.Trim().ToLowerInvariant() switch
        {
            "pricedesc" or "price_desc" => ProductSearchSort.PriceDesc,
            _ => ProductSearchSort.PriceAsc
        };

        var ps = pageSize ?? 20;
        // Backoffice admin search: by default surface all non-archived statuses (drafts included) so newly
        // created products are visible before publish. The eStore Status filter narrows this set.
        var statuses = MapAdminStatuses(status);
        var brandFilter = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim();

        // "More Filters": multi-category + quick-access toggles.
        var categoryIds = (categories ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .Distinct()
            .ToArray();
        var quick = (quickAccess ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToHashSet();
        // "0 Quantity" = total qty exactly 0; "Re-stock needed" = low-stock band (0 < qty <= threshold). Disjoint lists.
        var outOfStock = quick.Contains("zero-qty");
        var lowStock = quick.Contains("restock");
        var lowStockThreshold = configuration.GetValue<int?>("Catalog:LowStockThreshold") ?? 5;
        // "Sell on eStore" / "eStore Only": no dedicated channel field in the index — best
        // available signal is the product being live (status=active).
        if (quick.Contains("sell-on-estore") || quick.Contains("estore-only"))
            statuses = new[] { "active" };

        var result = await search
            .SearchAsync(new ProductSearchQuery(tenantId, storeId, q, inStock, category, ps, cursor, minPrice, maxPrice,
                sortEnum, attrFilters, facets == true, statuses, brandFilter,
                CategoryAncestorIds: categoryIds, OutOfStockOnly: outOfStock,
                LowStockOnly: lowStock, LowStockThreshold: lowStockThreshold), cancellationToken)
            .ConfigureAwait(false);
        var items = result.Items.Select(i => new
        {
            id = i.Id,
            name = i.Name,
            nameByLocale = i.NameByLocale,
            minBasePrice = new { amount = i.MinBasePrice.Amount, currency = i.MinBasePrice.Currency },
            hasInStockVariant = i.HasInStockVariant,
            slug = i.Slug,
            status = i.Status
        }).ToList();

        object meta = result.Facets is null
            ? new { totalCount = result.TotalCount, pageSize = ps, nextCursor = result.NextCursor }
            : new
            {
                totalCount = result.TotalCount,
                pageSize = ps,
                nextCursor = result.NextCursor,
                facets = new
                {
                    categories = result.Facets.CategoryAncestors.Select(b => new { id = b.Key, count = b.DocCount }),
                    priceMin = result.Facets.PriceMin,
                    priceMax = result.Facets.PriceMax,
                    attributeTerms = result.Facets.AttributeTerms
                }
            };
        return ApiEnvelope.Ok(items, meta);
    }

    /// <summary>DAI-658: products awaiting approval for the store (cursor = last <c>Id</c> from previous page).</summary>
    private static async Task<IResult> ListPendingApprovals(
        string tenantId,
        string storeId,
        ICatalogPersistence persistence,
        int? limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var lim = limit is > 0 ? Math.Min(limit.Value, 100) : 50;
        var after = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        var (items, total, nextCursor) = await persistence
            .ListPendingApprovalsForStoreAsync(tenantId, storeId, lim, after, cancellationToken)
            .ConfigureAwait(false);

        return ApiEnvelope.Ok(new
        {
            items = items.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                brandName = i.BrandName,
                categoryPath = i.CategoryPath,
                submittedByUserId = i.SubmittedByUserId,
                submittedAt = i.SubmittedAt,
                status = i.Status,
            }).ToList(),
            nextCursor,
            total,
        });
    }

    internal static bool TryParseAttributeFilters(string? raw, out IReadOnlyDictionary<string, string>? filters,
        out string? error)
    {
        filters = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        raw = raw.Trim();
        if (raw.Length > 4096)
        {
            error = "filters JSON is too large.";
            return false;
        }

        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (d is null || d.Count == 0)
                return true;
            if (d.Count > 24)
            {
                error = "Too many attribute filters.";
                return false;
            }

            foreach (var (k, v) in d)
            {
                if (string.IsNullOrWhiteSpace(k) || k.Length > 64 || v is null || v.Length > 256)
                {
                    error = "Invalid attribute filter key or value.";
                    return false;
                }
            }

            filters = d;
            return true;
        }
        catch (JsonException)
        {
            error = "filters must be a JSON object of string attributeId to string value.";
            return false;
        }
    }

    private static async Task<IResult> ListVariants(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            var list = await products.ListVariantsAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new
            {
                items = list.Select(v => new
                {
                    id = v.Id,
                    sku = v.Sku,
                    combinationHash = v.CombinationHash,
                    combinationCanonical = v.CombinationCanonical,
                    status = v.Status,
                    sortOrder = v.SortOrder,
                    basePriceAmount = v.BasePriceAmount
                }).ToList()
            });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> ListApprovalComments(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            var list = await products.ListApprovalCommentsAsync(productId, tenantId, storeId, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new
            {
                items = list.Select(c => new
                {
                    id = c.Id,
                    userId = c.UserId,
                    userName = c.UserId,
                    role = c.Role,
                    message = c.Message,
                    type = c.Type,
                    createdAt = c.CreatedAt
                }).ToList()
            });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> GetProduct(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        var p = await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (p is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        return ApiEnvelope.Ok(new
        {
            id = p.Id,
            tenantId = p.TenantId,
            storeId = p.StoreId,
            categoryId = p.CategoryId,
            brandId = p.BrandId,
            nameJson = p.NameJson,
            descriptionJson = p.DescriptionJson,
            slug = p.Slug,
            status = p.Status.ToPersistedValue(),
            salesCount30d = p.SalesCount30d,
            createdAt = p.CreatedAt,
            updatedAt = p.UpdatedAt,
            pageTitleJson = p.PageTitleJson,
            metaKeywordsJson = p.MetaKeywordsJson,
            metaDescriptionJson = p.MetaDescriptionJson,
            publishFrom = p.PublishFrom,
            publishUntil = p.PublishUntil,
            recommendSimilar = p.RecommendSimilar,
            recommendationsMode = p.RecommendationsMode,
            restockNotification = p.RestockNotification,
            customFieldsJson = p.CustomFieldsJson,
            attributes = Array.Empty<object>()
        });
    }

    private static async Task<(bool Ok, string? Json, string? Error)> NormalizeCustomFieldsAsync(
        JsonElement? raw,
        string tenantId,
        string storeId,
        IStoreProductFieldConfigPersistence fieldConfig,
        CancellationToken cancellationToken)
    {
        var (ok, json, error) = await ProductCustomFieldsNormalizer
            .NormalizeAsync(raw, tenantId, storeId, fieldConfig, cancellationToken)
            .ConfigureAwait(false);
        return ok ? (true, json, null) : (false, null, error);
    }

    /// <summary>Maps the posted metadata block to the domain value object (null when no metadata was supplied).</summary>
    private static ProductPageMetadata? ToMetadata(ProductPageMetadataBody? body)
    {
        if (body is null)
            return null;
        return new ProductPageMetadata(
            body.PageTitleJson,
            body.MetaKeywordsJson,
            body.MetaDescriptionJson,
            body.PublishFrom,
            body.PublishUntil,
            body.RecommendSimilar ?? true,
            body.RecommendationsMode,
            body.RestockNotification ?? false);
    }

    private static async Task<IResult> ListRecommendations(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        var rows = await products.ListRecommendationsAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        var items = rows.Select(r => new
        {
            id = r.RecommendedProductId,
            nameJson = r.NameJson,
            slug = r.Slug,
            status = r.Status,
            sortOrder = r.SortOrder
        });
        return ApiEnvelope.Ok(new { items });
    }

    private static async Task<IResult> SetRecommendations(
        string tenantId,
        string storeId,
        string productId,
        SetRecommendationsBody? body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            var ids = body?.RecommendedProductIds ?? new List<string>();
            var saved = await products
                .SetRecommendationsAsync(productId, tenantId, storeId, ids, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { items = saved });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
    }

    private static async Task<IResult> CreateProduct(
        string tenantId,
        string storeId,
        CreateProductBody body,
        ProductService products,
        IStoreProductFieldConfigPersistence fieldConfig,
        CancellationToken cancellationToken)
    {
        try
        {
            var (cfOk, cfJson, cfError) = await NormalizeCustomFieldsAsync(body.CustomFields, tenantId, storeId, fieldConfig,
                cancellationToken).ConfigureAwait(false);
            if (!cfOk)
                return ApiEnvelope.Error("validation_error", cfError!, StatusCodes.Status400BadRequest);

            var now = DateTimeOffset.UtcNow;
            var p = await products.CreateProductAsync(
                new CreateProductCommand(tenantId, storeId, body.CategoryId, body.NameJson, body.DescriptionJson ?? "{}",
                    body.Slug, body.BrandId, ToMetadata(body.Metadata), cfJson), now, cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = p.Id, slug = p.Slug, status = p.Status.ToPersistedValue() });
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (DuplicateProductSlugException ex)
        {
            return ApiEnvelope.Error("duplicate_slug", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> UpdateProduct(
        string tenantId,
        string storeId,
        string productId,
        UpdateProductBody body,
        ProductService products,
        IStoreProductFieldConfigPersistence fieldConfig,
        CancellationToken cancellationToken)
    {
        try
        {
            string? cfJson = null;
            if (body.CustomFields is not null)
            {
                var (cfOk, normalized, cfError) = await NormalizeCustomFieldsAsync(body.CustomFields, tenantId, storeId,
                    fieldConfig, cancellationToken).ConfigureAwait(false);
                if (!cfOk)
                    return ApiEnvelope.Error("validation_error", cfError!, StatusCodes.Status400BadRequest);
                cfJson = normalized;
            }

            var now = DateTimeOffset.UtcNow;
            await products.UpdateProductAsync(
                new UpdateProductCommand(productId, tenantId, storeId, body.CategoryId, body.NameJson,
                    body.DescriptionJson ?? "{}", body.Slug, body.BrandId, ToMetadata(body.Metadata), cfJson), now,
                cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (DuplicateProductSlugException ex)
        {
            return ApiEnvelope.Error("duplicate_slug", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> SubmitForApproval(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products
                .SubmitForApprovalAsync(productId, tenantId, storeId, ActorUserId(http.User), ActorCatalogRole(http.User),
                    DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.PendingApproval.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> SubmitForArchive(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products
                .SubmitForArchiveAsync(productId, tenantId, storeId, ActorUserId(http.User), ActorCatalogRole(http.User),
                    DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.PendingArchive.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> PostApprovalComment(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ApprovalCommentBody? body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body?.Message))
            return ApiEnvelope.Error("validation_error", "Message is required.", StatusCodes.Status400BadRequest);

        try
        {
            await products
                .AddApprovalCommentAsync(productId, tenantId, storeId, body.Message!, ActorUserId(http.User), ActorCatalogRole(http.User),
                    DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> PublishProduct(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
            if (product is null)
                return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

            var approvalRequired = await products.GetStoreApprovalRequiredAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
            if (approvalRequired && product.Status == ProductStatus.Draft && !CanBypassStoreApprovalGate(http.User))
            {
                return ApiEnvelope.Error("approval_required",
                    "This store requires submit-for-approval before publish (or use an elevated catalog role).",
                    StatusCodes.Status400BadRequest);
            }

            await products.PublishProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.Active.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static string PrimaryApprovalRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(DcmsRoles.SuperAdmin)) return DcmsRoles.SuperAdmin;
        if (user.IsInRole(DcmsRoles.ChainAdmin)) return DcmsRoles.ChainAdmin;
        if (user.IsInRole(DcmsRoles.BrandManager)) return DcmsRoles.BrandManager;
        return "approver";
    }

    private static async Task<IResult> ApprovePending(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var role = PrimaryApprovalRole(http.User);
        try
        {
            var resultStatus = await products
                .ApprovePendingProductAsync(productId, tenantId, storeId, userId, role, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = resultStatus.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> RequestChanges(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ApprovalActionCommentBody? body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body?.Comment))
            return ApiEnvelope.Error("validation_error", "Comment is required.", StatusCodes.Status400BadRequest);

        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var role = PrimaryApprovalRole(http.User);
        try
        {
            await products
                .RequestChangesOnPendingProductAsync(productId, tenantId, storeId, body.Comment!, userId, role,
                    DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.Draft.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> RejectPending(
        HttpContext http,
        string tenantId,
        string storeId,
        string productId,
        ApprovalActionCommentBody? body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body?.Comment))
            return ApiEnvelope.Error("validation_error", "Comment is required.", StatusCodes.Status400BadRequest);

        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var role = PrimaryApprovalRole(http.User);
        try
        {
            await products
                .RejectPendingProductAsync(productId, tenantId, storeId, body.Comment!, userId, role, DateTimeOffset.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.Draft.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> HideProduct(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products.HideProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.Hidden.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UnhideProduct(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products.UnhideProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.Active.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GenerateVariants(
        string tenantId,
        string storeId,
        string productId,
        GenerateVariantsBody body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (body.Axes is null || body.Axes.Count == 0)
            return ApiEnvelope.Error("validation_error", "Axes are required.", StatusCodes.Status400BadRequest);

        try
        {
            var axes = body.Axes
                .Select(a => new VariantAxisDef(a.AttributeId, a.ValueIds ?? new List<int>()))
                .ToList();
            var result = await products
                .GenerateVariantsAsync(
                    new GenerateVariantsCommand(productId, tenantId, storeId, axes, body.SkuPrefix ?? "sku"),
                    DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

            return ApiEnvelope.Ok(new
            {
                combinationCount = result.CombinationCount,
                inserted = result.Inserted,
                skippedDuplicates = result.SkippedDuplicates
            });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> ArchiveProduct(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products.ArchiveProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = productId, status = ProductStatus.Archived.ToPersistedValue() });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
    }

    /// <summary>US-9: REST <c>DELETE</c> maps to archive (soft remove) for store catalog.</summary>
    private static Task<IResult> DeleteProductAsArchive(string tenantId, string storeId, string productId,
        ProductService products, CancellationToken cancellationToken) =>
        ArchiveProduct(tenantId, storeId, productId, products, cancellationToken);

    private static async Task<IResult> BulkPostProducts(
        string tenantId,
        string storeId,
        BulkPostBody body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        var createCount = body.Items?.Count ?? 0;
        var priceCount = body.VariantPrices?.Count ?? 0;
        if (createCount > 0 && priceCount > 0)
            return ApiEnvelope.Error("validation_error", "Send either items (create) or variantPrices, not both.",
                StatusCodes.Status400BadRequest);
        if (createCount == 0 && priceCount == 0)
            return ApiEnvelope.Error("validation_error", "Items or variantPrices is required.", StatusCodes.Status400BadRequest);

        if (priceCount > 0)
            return await BulkUpdateVariantPrices(tenantId, storeId, body.VariantPrices!, products, cancellationToken)
                .ConfigureAwait(false);

        if (createCount > BulkMaxItems)
            return ApiEnvelope.Error("validation_error", $"At most {BulkMaxItems} items per request.",
                StatusCodes.Status400BadRequest);

        var succeeded = new List<object>();
        var failed = new List<object>();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < body.Items!.Count; i++)
        {
            var item = body.Items[i];
            try
            {
                var p = await products.CreateProductAsync(
                    new CreateProductCommand(tenantId, storeId, item.CategoryId, item.NameJson,
                        item.DescriptionJson ?? "{}", item.Slug), now, cancellationToken).ConfigureAwait(false);
                succeeded.Add(new { index = i, id = p.Id, slug = p.Slug, status = p.Status.ToPersistedValue() });
            }
            catch (Exception ex) when (ex is ArgumentException or DuplicateProductSlugException)
            {
                var code = ex is DuplicateProductSlugException ? "duplicate_slug" : "validation_error";
                failed.Add(new { index = i, code, message = ex.Message });
            }
        }

        return ApiEnvelope.Ok(new { succeeded, failed },
            new { requested = body.Items.Count, succeeded = succeeded.Count, failed = failed.Count });
    }

    private static async Task<IResult> BulkUpdateVariantPrices(
        string tenantId,
        string storeId,
        List<BulkVariantPriceRow> rows,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (rows.Count > BulkMaxItems)
            return ApiEnvelope.Error("validation_error", $"At most {BulkMaxItems} variant price rows per request.",
                StatusCodes.Status400BadRequest);

        var now = DateTimeOffset.UtcNow;
        var items = rows.Select(r => (r.ProductId, r.VariantId, r.BasePriceAmount)).ToList();
        var (succeeded, failed) =
            await products.BulkUpdateVariantPricesAsync(tenantId, storeId, items, now, cancellationToken)
                .ConfigureAwait(false);
        return ApiEnvelope.Ok(new { succeeded, failed },
            new { requested = rows.Count, succeeded = succeeded.Count, failed = failed.Count });
    }

    private static async Task<IResult> BulkUpdateProducts(
        string tenantId,
        string storeId,
        BulkUpdateBody body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (body.Items is null || body.Items.Count == 0)
            return ApiEnvelope.Error("validation_error", "Items are required.", StatusCodes.Status400BadRequest);
        if (body.Items.Count > BulkMaxItems)
            return ApiEnvelope.Error("validation_error", $"At most {BulkMaxItems} items per request.",
                StatusCodes.Status400BadRequest);

        var succeeded = new List<object>();
        var failed = new List<object>();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < body.Items.Count; i++)
        {
            var item = body.Items[i];
            try
            {
                await products.UpdateProductAsync(
                    new UpdateProductCommand(item.ProductId, tenantId, storeId, item.CategoryId, item.NameJson,
                        item.DescriptionJson ?? "{}", item.Slug), now, cancellationToken).ConfigureAwait(false);
                succeeded.Add(new { index = i, id = item.ProductId });
            }
            catch (Exception ex) when (ex is ArgumentException or DuplicateProductSlugException or ProductNotFoundException
                     or InvalidProductStateException)
            {
                var code = ex switch
                {
                    ProductNotFoundException => "not_found",
                    DuplicateProductSlugException => "duplicate_slug",
                    InvalidProductStateException => "invalid_state",
                    _ => "validation_error"
                };
                failed.Add(new { index = i, code, message = ex.Message });
            }
        }

        return ApiEnvelope.Ok(new { succeeded, failed },
            new { requested = body.Items.Count, succeeded = succeeded.Count, failed = failed.Count });
    }

    private static async Task<IResult> CreateManualVariant(
        string tenantId,
        string storeId,
        string productId,
        CreateManualVariantBody body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            var v = await products
                .CreateManualVariantAsync(productId, tenantId, storeId, body.Sku ?? "", body.CombinationHash ?? "",
                    body.CombinationCanonical, body.BasePriceAmount ?? 0, body.Status ?? "active", DateTimeOffset.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new
            {
                id = v.Id,
                productId = v.ProductId,
                sku = v.Sku,
                combinationHash = v.CombinationHash,
                combinationCanonical = v.CombinationCanonical,
                status = v.Status,
                sortOrder = v.SortOrder,
                basePriceAmount = v.BasePriceAmount
            });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidProductStateException ex)
        {
            return ApiEnvelope.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (DuplicateVariantSkuException ex)
        {
            return ApiEnvelope.Error("duplicate_variant_sku", ex.Message, StatusCodes.Status409Conflict);
        }
        catch (DuplicateVariantCombinationHashException ex)
        {
            return Results.Json(
                new
                {
                    data = (object?)null,
                    meta = new { conflictingVariantId = ex.ConflictingVariantId, combinationHash = body.CombinationHash },
                    error = new { code = "duplicate_combination_hash", message = ex.Message }
                },
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> UpdateVariant(
        string tenantId,
        string storeId,
        string productId,
        string variantId,
        UpdateVariantBody body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products.UpdateVariantAsync(variantId, productId, tenantId, storeId, body.Sku, body.Status,
                body.SortOrder, body.BasePriceAmount, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = variantId, productId });
        }
        catch (ProductNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (DuplicateVariantSkuException ex)
        {
            return ApiEnvelope.Error("duplicate_variant_sku", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    public sealed record CreateProductBody(int CategoryId, string NameJson, string? DescriptionJson, string Slug,
        string? BrandId = null, ProductPageMetadataBody? Metadata = null, JsonElement? CustomFields = null);

    public sealed record UpdateProductBody(int CategoryId, string NameJson, string? DescriptionJson, string Slug,
        string? BrandId = null, ProductPageMetadataBody? Metadata = null, JsonElement? CustomFields = null);

    /// <summary>Product Page / SEO metadata + visibility flags posted from the backoffice product editor.</summary>
    public sealed record ProductPageMetadataBody(
        string? PageTitleJson,
        string? MetaKeywordsJson,
        string? MetaDescriptionJson,
        DateTimeOffset? PublishFrom,
        DateTimeOffset? PublishUntil,
        bool? RecommendSimilar,
        string? RecommendationsMode,
        bool? RestockNotification);

    public sealed record SetRecommendationsBody(List<string>? RecommendedProductIds);

    public sealed record GenerateVariantsBody(List<VariantAxisJson>? Axes, string? SkuPrefix);

    public sealed record VariantAxisJson(int AttributeId, List<int>? ValueIds);

    public sealed record BulkPostBody(List<CreateProductBody>? Items, List<BulkVariantPriceRow>? VariantPrices);

    public sealed record BulkVariantPriceRow(string ProductId, string VariantId, long BasePriceAmount);

    public sealed record BulkUpdateItem(string ProductId, int CategoryId, string NameJson, string? DescriptionJson,
        string Slug);

    public sealed record BulkUpdateBody(List<BulkUpdateItem>? Items);

    public sealed record UpdateVariantBody(string? Sku, string? Status, int? SortOrder, long? BasePriceAmount);

    public sealed record CreateManualVariantBody(string? Sku, string? CombinationHash, string? CombinationCanonical,
        long? BasePriceAmount, string? Status);

    public sealed record ApprovalActionCommentBody(string? Comment);

    public sealed record ApprovalCommentBody(string? Message);
}
