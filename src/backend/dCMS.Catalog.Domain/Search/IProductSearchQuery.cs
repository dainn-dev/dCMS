namespace dCMS.Core.Search;

/// <summary>Storefront/catalog product search (US-6 stock + US-7 filters/facets/perf).</summary>
public interface IProductSearchQuery
{
    Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
}

public sealed record ProductSearchQuery(
    string TenantId,
    string StoreId,
    string? Keyword,
    bool? InStockOnly,
    int? CategoryAncestorId,
    int PageSize,
    string? SearchAfterCursor,
    long? MinPriceAmount = null,
    long? MaxPriceAmount = null,
    ProductSearchSort Sort = ProductSearchSort.PriceAsc,
    IReadOnlyDictionary<string, string>? AttributeFilters = null,
    bool IncludeFacets = false,
    /// <summary>Persisted status values to include (e.g. "active", "draft"). Null/empty → storefront default of "active" only.</summary>
    IReadOnlyList<string>? Statuses = null,
    /// <summary>Optional brand code to restrict results to a single brand.</summary>
    string? BrandId = null,
    /// <summary>Product custom-field properties (by snake_case key) to aggregate when <see cref="IncludeFacets"/> is true.</summary>
    IReadOnlyList<string>? CustomFieldFacetProperties = null,
    /// <summary>Multi-category filter (matches products whose ancestors contain ANY of these ids). Unions with <see cref="CategoryAncestorId"/>.</summary>
    IReadOnlyList<int>? CategoryAncestorIds = null,
    /// <summary>Quick-access "0 Quantity" filter — matches products whose total available quantity is exactly 0.</summary>
    bool OutOfStockOnly = false,
    /// <summary>Quick-access "Re-stock needed" filter — matches products with low stock (0 &lt; total qty ≤ <see cref="LowStockThreshold"/>).</summary>
    bool LowStockOnly = false,
    /// <summary>Inclusive upper bound for the "Re-stock needed" low-stock band. Defaults to 5 when not configured.</summary>
    int LowStockThreshold = 5);

public sealed record ProductSearchItem(
    string Id,
    string Name,
    IReadOnlyDictionary<string, string> NameByLocale,
    MoneyAmount MinBasePrice,
    bool HasInStockVariant,
    string Slug,
    string Status = "active");

public sealed record FacetTermBucket(string Key, long DocCount);

public sealed record SearchFacets(
    IReadOnlyList<FacetTermBucket> CategoryAncestors,
    IReadOnlyDictionary<string, IReadOnlyList<FacetTermBucket>> AttributeTerms,
    long? PriceMin,
    long? PriceMax);

public sealed record ProductSearchResult(
    IReadOnlyList<ProductSearchItem> Items,
    long TotalCount,
    string? NextCursor,
    SearchFacets? Facets = null);
