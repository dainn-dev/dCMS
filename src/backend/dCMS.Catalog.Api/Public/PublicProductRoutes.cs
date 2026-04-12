using System.Text.Json;
using dCMS.Catalog.Api.Http;
using dCMS.Catalog.Api.Products;
using dCMS.Core.Caching;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Core.Services;
namespace dCMS.Catalog.Api.Public;

/// <summary>US-10: anonymous storefront product search + detail + slug check (scoped by tenantId + storeId query).</summary>
public static class PublicProductRoutes
{
    private const int ScopeIdMaxLength = 64;
    private static readonly TimeSpan DetailCacheTtl = TimeSpan.FromMinutes(10);

    public static void MapPublicProductRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/products")
            .WithTags("catalog-public-products")
            .AllowAnonymous();

        g.MapGet("", PublicSearchProducts);
        g.MapGet("slug-check", PublicSlugCheck).RequireRateLimiting("PublicSlugCheck");
        g.MapGet("{slug}", PublicGetProductBySlug);
    }

    private static bool TryValidateTenantStore(string? tenantId, string? storeId, out string tenant, out string store,
        out IResult? error)
    {
        tenant = "";
        store = "";
        error = null;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId))
        {
            error = ApiEnvelope.Error("validation_error", "Query parameters tenantId and storeId are required.",
                StatusCodes.Status400BadRequest);
            return false;
        }

        tenant = tenantId.Trim();
        store = storeId.Trim();
        if (tenant.Length == 0 || tenant.Length > ScopeIdMaxLength || store.Length == 0 ||
            store.Length > ScopeIdMaxLength)
        {
            error = ApiEnvelope.Error("validation_error", "Invalid tenantId or storeId.", StatusCodes.Status400BadRequest);
            return false;
        }

        return true;
    }

    private static async Task<IResult> PublicSearchProducts(
        HttpContext http,
        IProductSearchQuery search,
        string? tenantId,
        string? storeId,
        string? q,
        bool? inStock,
        int? categoryId,
        int? category,
        int? pageSize,
        string? cursor,
        long? minPrice,
        long? maxPrice,
        string? sort,
        string? sortBy,
        bool? facets,
        string? filters,
        CancellationToken cancellationToken)
    {
        if (!TryValidateTenantStore(tenantId, storeId, out var tenant, out var store, out var err))
            return err!;

        http.Response.Headers.CacheControl = "public, max-age=30";

        if (!ProductRoutes.TryParseAttributeFilters(filters, out var attrFilters, out var filterError))
            return ApiEnvelope.Error("validation_error", filterError ?? "Invalid filters.", StatusCodes.Status400BadRequest);

        if (minPrice is < 0 || maxPrice is < 0 || (minPrice is not null && maxPrice is not null && minPrice > maxPrice))
            return ApiEnvelope.Error("validation_error", "Invalid price range.", StatusCodes.Status400BadRequest);

        var sortRaw = (sortBy ?? sort)?.Trim().ToLowerInvariant();
        var sortEnum = sortRaw switch
        {
            "pricedesc" or "price_desc" => ProductSearchSort.PriceDesc,
            _ => ProductSearchSort.PriceAsc
        };

        var cat = categoryId ?? category;
        var ps = pageSize ?? 20;
        var result = await search
            .SearchAsync(new ProductSearchQuery(tenant, store, q, inStock, cat, ps, cursor, minPrice, maxPrice,
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

    private static async Task<IResult> PublicSlugCheck(
        ICatalogPersistence persistence,
        string? tenantId,
        string? storeId,
        string? slug,
        CancellationToken cancellationToken)
    {
        if (!TryValidateTenantStore(tenantId, storeId, out var tenant, out var store, out var err))
            return err!;

        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 256)
            return ApiEnvelope.Error("validation_error", "slug query parameter is required (max 256 chars).",
                StatusCodes.Status400BadRequest);

        var normalized = slug.Trim().ToLowerInvariant();
        var taken = await persistence.SlugExistsAsync(store, normalized, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { available = !taken, slug = normalized, tenantId = tenant, storeId = store });
    }

    private static async Task<IResult> PublicGetProductBySlug(
        HttpContext http,
        ICatalogPersistence persistence,
        ProductService products,
        IProductPublicDetailCache detailCache,
        string slug,
        string? tenantId,
        string? storeId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(slug, "slug-check", StringComparison.OrdinalIgnoreCase))
            return ApiEnvelope.Error("not_found", "Not found.", StatusCodes.Status404NotFound);

        if (!TryValidateTenantStore(tenantId, storeId, out var tenant, out var store, out var err))
            return err!;

        http.Response.Headers.CacheControl = "public, max-age=60";

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (normalizedSlug.Length == 0 || normalizedSlug.Length > 256)
            return ApiEnvelope.Error("validation_error", "Invalid slug.", StatusCodes.Status400BadRequest);

        var cached = await detailCache.TryGetAsync(store, normalizedSlug, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(cached) &&
            TryParseDetailCache(cached, out var versionUnix, out var productId, out var dataJson))
        {
            var etag = BuildEtag(productId, versionUnix);
            http.Response.Headers.ETag = etag;
            if (EtagsMatch(http.Request.Headers.IfNoneMatch.ToString(), etag))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            var dataObj = JsonSerializer.Deserialize<object>(dataJson);
            return ApiEnvelope.Ok(dataObj);
        }

        var product = await persistence.GetBySlugAsync(store, tenant, normalizedSlug, cancellationToken)
            .ConfigureAwait(false);
        if (product is null || product.Status != ProductStatus.Active)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var variants = await products.ListVariantsAsync(product.Id, tenant, store, cancellationToken)
            .ConfigureAwait(false);

        var vu = product.UpdatedAt.ToUnixTimeSeconds();
        var body = BuildPublicProductBody(product, variants);
        var etagLive = BuildEtag(product.Id, vu);
        http.Response.Headers.ETag = etagLive;
        if (EtagsMatch(http.Request.Headers.IfNoneMatch.ToString(), etagLive))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        var envelopeJson = JsonSerializer.Serialize(new { versionUnix = vu, data = body });
        await detailCache.SetAsync(store, normalizedSlug, envelopeJson, DetailCacheTtl, cancellationToken)
            .ConfigureAwait(false);

        return ApiEnvelope.Ok(body);
    }

    private static bool TryParseDetailCache(string json, out long versionUnix, out string productId,
        out string dataJson)
    {
        versionUnix = 0;
        productId = "";
        dataJson = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            versionUnix = root.GetProperty("versionUnix").GetInt64();
            var data = root.GetProperty("data");
            if (!data.TryGetProperty("id", out var idEl))
                return false;
            productId = idEl.GetString() ?? "";
            if (productId.Length == 0)
                return false;
            dataJson = data.GetRawText();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildEtag(string rootProductId, long versionUnix) =>
        $"W/\"product-{rootProductId}-v{versionUnix}\"";

    private static bool EtagsMatch(string? ifNoneMatch, string etag)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
            return false;
        var normalized = NormalizeEtag(etag);
        foreach (var part in ifNoneMatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (NormalizeEtag(part) == normalized)
                return true;
        }

        return false;
    }

    private static string NormalizeEtag(string raw)
    {
        var t = raw.Trim().Trim('"');
        if (t.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            t = t[2..].Trim().Trim('"');
        return t;
    }

    private static object BuildPublicProductBody(Product product, IReadOnlyList<ProductVariant> variants)
    {
        var combinations = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var v in variants)
        {
            var key = string.IsNullOrEmpty(v.CombinationCanonical) ? v.CombinationHash : v.CombinationCanonical;
            combinations[key] = new
            {
                variantId = v.Id,
                sku = v.Sku,
                basePriceAmount = v.BasePriceAmount,
                inStock = string.Equals(v.Status, "active", StringComparison.OrdinalIgnoreCase)
            };
        }

        return new
        {
            id = product.Id,
            tenantId = product.TenantId,
            storeId = product.StoreId,
            categoryId = product.CategoryId,
            name = JsonSerializer.Deserialize<object>(product.NameJson),
            description = JsonSerializer.Deserialize<object>(product.DescriptionJson),
            slug = product.Slug,
            status = product.Status.ToPersistedValue(),
            variantMatrix = new
            {
                axes = Array.Empty<object>(),
                combinations
            }
        };
    }
}
