using StackExchange.Redis;

namespace dCMS.Order.Infrastructure.Caching;

public sealed class RedisOrderDetailCache(IConnectionMultiplexer mux) : IOrderDetailCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<string?> GetDetailJsonAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var v = await mux.GetDatabase().StringGetAsync(CacheKey(orderId)).ConfigureAwait(false);
        return v.HasValue ? (string?)v : null;
    }

    public Task SetDetailJsonAsync(string orderId, string json, CancellationToken cancellationToken = default) =>
        mux.GetDatabase().StringSetAsync(CacheKey(orderId), json, Ttl);

    public Task InvalidateAsync(string orderId, CancellationToken cancellationToken = default) =>
        mux.GetDatabase().KeyDeleteAsync(CacheKey(orderId));

    private static string CacheKey(string orderId) => "dcms:order:" + orderId;
}
