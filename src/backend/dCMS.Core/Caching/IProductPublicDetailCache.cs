namespace dCMS.Core.Caching;

/// <summary>Redis cache-aside for public GET product-by-slug (US-10). Key <c>dcms:product:{storeId}:{slug}</c>, TTL 10m.</summary>
public interface IProductPublicDetailCache
{
    /// <summary>Returns cached JSON envelope payload (including embedded etag parts) or null.</summary>
    Task<string?> TryGetAsync(string storeId, string slug, CancellationToken cancellationToken = default);

    Task SetAsync(string storeId, string slug, string jsonPayload, TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
