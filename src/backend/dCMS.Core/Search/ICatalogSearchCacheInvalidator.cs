namespace dCMS.Core.Search;

/// <summary>Invalidates storefront search caches after catalog index changes (Redis keys per AC).</summary>
public interface ICatalogSearchCacheInvalidator
{
    /// <summary>
    /// <c>DEL dcms:product:&#123;storeId&#125;:&#123;slug&#125;</c> when slug is known, plus
    /// <c>SCAN/DEL dcms:search:&#123;storeId&#125;:*</c>.
    /// </summary>
    Task InvalidateAfterIndexChangeAsync(string storeId, string? slug, CancellationToken cancellationToken = default);
}
