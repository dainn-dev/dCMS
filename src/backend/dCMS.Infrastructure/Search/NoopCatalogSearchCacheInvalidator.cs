using dCMS.Core.Search;

namespace dCMS.Infrastructure.Search;

public sealed class NoopCatalogSearchCacheInvalidator : ICatalogSearchCacheInvalidator
{
    public static readonly NoopCatalogSearchCacheInvalidator Instance = new();

    public Task InvalidateAfterIndexChangeAsync(string storeId, string? slug, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
