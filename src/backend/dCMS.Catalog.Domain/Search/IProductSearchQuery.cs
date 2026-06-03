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
    IReadOnlyList<string>? CustomFieldFacetProperties = null);

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
