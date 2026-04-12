using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Commands;
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
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
        Auth(g.MapGet("{productId}/variants", ListVariants), auth, write: false);
        Auth(g.MapGet("{productId}", GetProduct), auth, write: false);
        Auth(g.MapPost("bulk", BulkCreateProducts), auth, write: true);
        Auth(g.MapPut("bulk", BulkUpdateProducts), auth, write: true);
        Auth(g.MapPost("", CreateProduct), auth, write: true);
        Auth(g.MapPut("{productId}", UpdateProduct), auth, write: true);
        Auth(g.MapPatch("{productId}", UpdateProduct), auth, write: true);
        Auth(g.MapDelete("{productId}", DeleteProductAsArchive), auth, write: true);
        Auth(g.MapPost("{productId}/submit-for-approval", SubmitForApproval), auth, write: true);
        Auth(g.MapPost("{productId}/publish", PublishProduct), auth, write: true);
        Auth(g.MapPost("{productId}/hide", HideProduct), auth, write: true);
        Auth(g.MapPost("{productId}/unhide", UnhideProduct), auth, write: true);
        Auth(g.MapPost("{productId}/archive", ArchiveProduct), auth, write: true);
        Auth(g.MapPost("{productId}/variants/generate", GenerateVariants), auth, write: true);
        Auth(g.MapPut("{productId}/variants/{variantId}", UpdateVariant), auth, write: true);
    }

    private const int BulkMaxItems = 100;

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder builder, bool authEnabled, bool write) =>
        authEnabled
            ? builder.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : builder;

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
        var result = await search
            .SearchAsync(new ProductSearchQuery(tenantId, storeId, q, inStock, category, ps, cursor, minPrice, maxPrice,
                sortEnum, attrFilters, facets == true), cancellationToken)
            .ConfigureAwait(false);
        var items = result.Items.Select(i => new
        {
            id = i.Id,
            name = i.Name,
            nameByLocale = i.NameByLocale,
            minBasePrice = new { amount = i.MinBasePrice.Amount, currency = i.MinBasePrice.Currency },
            hasInStockVariant = i.HasInStockVariant,
            slug = i.Slug
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
                items = list.Select(v => new { id = v.Id, sku = v.Sku, combinationHash = v.CombinationHash, status = v.Status, sortOrder = v.SortOrder }).ToList()
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
            nameJson = p.NameJson,
            descriptionJson = p.DescriptionJson,
            slug = p.Slug,
            status = p.Status.ToPersistedValue(),
            salesCount30d = p.SalesCount30d,
            createdAt = p.CreatedAt,
            updatedAt = p.UpdatedAt,
            attributes = Array.Empty<object>()
        });
    }

    private static async Task<IResult> CreateProduct(
        string tenantId,
        string storeId,
        CreateProductBody body,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var p = await products.CreateProductAsync(
                new CreateProductCommand(tenantId, storeId, body.CategoryId, body.NameJson, body.DescriptionJson ?? "{}",
                    body.Slug), now, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            await products.UpdateProductAsync(
                new UpdateProductCommand(productId, tenantId, storeId, body.CategoryId, body.NameJson,
                    body.DescriptionJson ?? "{}", body.Slug), now, cancellationToken).ConfigureAwait(false);
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
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
            await products.SubmitForApprovalAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
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

    private static async Task<IResult> PublishProduct(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        try
        {
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

    private static async Task<IResult> BulkCreateProducts(
        string tenantId,
        string storeId,
        BulkCreateBody body,
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
                body.SortOrder, cancellationToken).ConfigureAwait(false);
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

    public sealed record CreateProductBody(int CategoryId, string NameJson, string? DescriptionJson, string Slug);

    public sealed record UpdateProductBody(int CategoryId, string NameJson, string? DescriptionJson, string Slug);

    public sealed record GenerateVariantsBody(List<VariantAxisJson>? Axes, string? SkuPrefix);

    public sealed record VariantAxisJson(int AttributeId, List<int>? ValueIds);

    public sealed record BulkCreateBody(List<CreateProductBody>? Items);

    public sealed record BulkUpdateItem(string ProductId, int CategoryId, string NameJson, string? DescriptionJson,
        string Slug);

    public sealed record BulkUpdateBody(List<BulkUpdateItem>? Items);

    public sealed record UpdateVariantBody(string? Sku, string? Status, int? SortOrder);
}
