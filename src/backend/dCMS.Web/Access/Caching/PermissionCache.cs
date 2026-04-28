using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace dCMS.Web.Access.Caching;

/// <summary>
/// DAI-683 — Permission cache with Redis backing + in-memory fallback.
/// </summary>
public sealed class PermissionCache : IPermissionCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer? _redis;
    private readonly IMemoryCache _memory;
    private readonly PermissionCacheOptions _options;
    private readonly ILogger<PermissionCache> _logger;

    public PermissionCache(
        IConnectionMultiplexer? redis,
        IMemoryCache memory,
        IOptions<PermissionCacheOptions> options,
        ILogger<PermissionCache> logger)
    {
        _redis = redis;
        _memory = memory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>?> TryGetGrantedActionsAsync(
        string tenantId,
        int userId,
        string rolesHash,
        string module,
        CancellationToken ct = default)
    {
        var key = await BuildKeyAsync(tenantId, userId, rolesHash, module, ct).ConfigureAwait(false);
        if (_memory.TryGetValue(key, out string[]? cachedMem) && cachedMem is not null)
        {
            PermissionCacheMetrics.Hits.WithLabels("memory").Inc();
            return cachedMem;
        }

        if (_redis is null)
        {
            PermissionCacheMetrics.Misses.Inc();
            return null;
        }

        try
        {
            var raw = await _redis.GetDatabase().StringGetAsync(key).ConfigureAwait(false);
            if (!raw.HasValue)
            {
                PermissionCacheMetrics.Misses.Inc();
                return null;
            }

            var actions = JsonSerializer.Deserialize<string[]>(raw!, Json) ?? Array.Empty<string>();
            _memory.Set(key, actions, _options.MemoryTtl);
            PermissionCacheMetrics.Hits.WithLabels("redis").Inc();
            return actions;
        }
        catch (Exception ex)
        {
            // Graceful degradation: treat Redis failure as cache-miss, rely on in-memory fallback set on write.
            PermissionCacheMetrics.Errors.WithLabels("read").Inc();
            _logger.LogWarning(ex, "Permission cache Redis read failed for tenant={TenantId} userId={UserId} module={Module}", tenantId, userId, module);
            return null;
        }
    }

    public async Task SetGrantedActionsAsync(
        string tenantId,
        int userId,
        string rolesHash,
        string module,
        IReadOnlyList<string> actions,
        CancellationToken ct = default)
    {
        var key = await BuildKeyAsync(tenantId, userId, rolesHash, module, ct).ConfigureAwait(false);
        var arr = actions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();

        _memory.Set(key, arr, _options.MemoryTtl);

        if (_redis is null)
            return;

        try
        {
            var payload = JsonSerializer.Serialize(arr, Json);
            await _redis.GetDatabase().StringSetAsync(key, payload, _options.RedisTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PermissionCacheMetrics.Errors.WithLabels("write").Inc();
            _logger.LogWarning(ex, "Permission cache Redis write failed for tenant={TenantId} userId={UserId} module={Module}", tenantId, userId, module);
        }
    }

    public async Task BumpTenantVersionAsync(string tenantId, CancellationToken ct = default)
    {
        // Invalidate the L1 version cache so the next BuildKeyAsync re-reads from Redis.
        _memory.Remove(VersionMemoryKey(tenantId));

        if (_redis is null)
            return;

        try
        {
            var vKey = $"{_options.KeyPrefix}:v:{tenantId}";
            await _redis.GetDatabase().StringIncrementAsync(vKey).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PermissionCacheMetrics.Errors.WithLabels("version").Inc();
            _logger.LogWarning(ex, "Permission cache Redis version bump failed for tenant={TenantId}", tenantId);
        }
    }

    private async Task<string> BuildKeyAsync(string tenantId, int userId, string rolesHash, string module, CancellationToken ct)
    {
        tenantId = string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId.Trim();
        module = (module ?? "").Trim().ToLowerInvariant();

        var version = await GetTenantVersionAsync(tenantId).ConfigureAwait(false);

        return $"{_options.KeyPrefix}:{tenantId}:{userId}:{rolesHash}:{version}:{module}";
    }

    private async Task<long> GetTenantVersionAsync(string tenantId)
    {
        var memKey = VersionMemoryKey(tenantId);
        if (_memory.TryGetValue(memKey, out long cached))
            return cached;

        if (_redis is null)
        {
            _memory.Set(memKey, 0L, _options.VersionMemoryTtl);
            return 0L;
        }

        long version = 0;
        try
        {
            var vKey = $"{_options.KeyPrefix}:v:{tenantId}";
            var raw = await _redis.GetDatabase().StringGetAsync(vKey).ConfigureAwait(false);
            if (raw.HasValue && long.TryParse(raw.ToString(), out var v))
                version = v;
        }
        catch
        {
            PermissionCacheMetrics.Errors.WithLabels("version").Inc();
            // ignore version read failures — fall through with version=0
        }

        _memory.Set(memKey, version, _options.VersionMemoryTtl);
        return version;
    }

    private string VersionMemoryKey(string tenantId) => $"{_options.KeyPrefix}:vmem:{tenantId}";
}

