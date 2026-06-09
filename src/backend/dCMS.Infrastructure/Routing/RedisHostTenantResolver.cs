using StackExchange.Redis;

namespace dCMS.Infrastructure.Routing;

public sealed class RedisHostTenantResolver(IConnectionMultiplexer redis) : IHostTenantResolver
{
    private readonly IConnectionMultiplexer _redis = redis;

    public async Task<HostTenantResolution?> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        try
        {
            var raw = await _redis.GetDatabase()
                .StringGetAsync("dcms:host:" + host.Trim().ToLowerInvariant())
                .ConfigureAwait(false);
            if (!raw.HasValue)
                return null;

            var parts = raw.ToString().Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                return null;

            return new HostTenantResolution(parts[0], parts[1]);
        }
        catch
        {
            return null;
        }
    }
}
