using System.Text.Json;
using dCMS.Core.Models;
using StackExchange.Redis;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Redis cache for active campaigns per (tenantId, hour-bucket).
/// 5-minute TTL — long enough to avoid hot-path DB hits, short enough that workflow
/// transitions to/from "active" become visible without explicit invalidation.
/// </summary>
public sealed class ActiveCampaignsCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<ActiveCampaignsCache> _logger;

    public ActiveCampaignsCache(IConnectionMultiplexer? redis, ILogger<ActiveCampaignsCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CampaignRow>?> TryGetAsync(string tenantId, DateTimeOffset now, CancellationToken ct)
    {
        if (_redis is null) return null;
        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(Key(tenantId, now)).WaitAsync(ct);
            if (!raw.HasValue) return null;
            return JsonSerializer.Deserialize<List<CampaignRow>>(raw!) ?? new List<CampaignRow>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active-campaign cache read failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    public async Task SetAsync(string tenantId, DateTimeOffset now, IReadOnlyList<CampaignRow> rows, CancellationToken ct)
    {
        if (_redis is null) return;
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(rows);
            await db.StringSetAsync(Key(tenantId, now), json, Ttl).WaitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active-campaign cache write failed for tenant {TenantId}", tenantId);
        }
    }

    private static string Key(string tenantId, DateTimeOffset now) =>
        $"promo:campaigns:{tenantId}:{now.UtcDateTime:yyyyMMddHH}";
}
