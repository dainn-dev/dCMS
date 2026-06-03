using dCMS.Core.Search;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Search;

/// <summary>
/// Redis: <c>DEL dcms:product:&#123;storeId&#125;:&#123;slug&#125;</c> and <c>SCAN/DEL dcms:search:&#123;storeId&#125;:*</c>.
/// </summary>
public sealed class RedisCatalogSearchCacheInvalidator(IConnectionMultiplexer multiplexer) : ICatalogSearchCacheInvalidator
{
    private readonly IConnectionMultiplexer _multiplexer =
        multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));

    public async Task InvalidateAfterIndexChangeAsync(string storeId, string? slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);
        var db = _multiplexer.GetDatabase();

        if (!string.IsNullOrWhiteSpace(slug))
            await db.KeyDeleteAsync($"dcms:product:{storeId}:{slug}").ConfigureAwait(false);

        foreach (var endpoint in _multiplexer.GetEndPoints())
        {
            var server = _multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
                continue;

            // Prefer SCAN-style enumeration (pageSize); avoid blocking KEYS * on large DBs.
            foreach (var key in server.Keys(database: db.Database, pattern: $"dcms:search:{storeId}:*", pageSize: 256))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }
    }

    public async Task InvalidateStoreAsync(string storeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);
        var db = _multiplexer.GetDatabase();

        foreach (var endpoint in _multiplexer.GetEndPoints())
        {
            var server = _multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
                continue;

            foreach (var key in server.Keys(database: db.Database, pattern: $"dcms:product:{storeId}:*", pageSize: 256))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            }

            foreach (var key in server.Keys(database: db.Database, pattern: $"dcms:search:{storeId}:*", pageSize: 256))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }
    }
}
