using dCMS.Core.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Caching;

public sealed class RedisProductPublicDetailCache(
    IConnectionMultiplexer redis,
    ILogger<RedisProductPublicDetailCache> logger) : IProductPublicDetailCache
{
    public static string RedisKey(string storeId, string slug) => $"dcms:product:{storeId}:{slug}";

    public async Task<string?> TryGetAsync(string storeId, string slug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var v = await redis.GetDatabase().StringGetAsync(RedisKey(storeId, slug)).ConfigureAwait(false);
            return v.HasValue ? v.ToString() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis public product cache read failed for {StoreId}/{Slug}.", storeId, slug);
            return null;
        }
    }

    public async Task SetAsync(string storeId, string slug, string jsonPayload, TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().StringSetAsync(RedisKey(storeId, slug), jsonPayload, ttl).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis public product cache write failed for {StoreId}/{Slug}.", storeId, slug);
        }
    }
}
