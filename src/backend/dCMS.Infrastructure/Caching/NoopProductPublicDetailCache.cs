using dCMS.Core.Caching;

namespace dCMS.Infrastructure.Caching;

public sealed class NoopProductPublicDetailCache : IProductPublicDetailCache
{
    public Task<string?> TryGetAsync(string storeId, string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task SetAsync(string storeId, string slug, string jsonPayload, TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
