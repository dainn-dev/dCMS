using System.Text.Json;
using dCMS.Core.Models;
using StackExchange.Redis;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// DAI-692: hot cache for promo-code binding rows. 60-second TTL — short enough that
/// workflow-state changes propagate without explicit invalidation.
/// Caps are NOT cached (must remain authoritative against the redemptions table).
/// </summary>
public sealed class PromoCodeCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<PromoCodeCache> _logger;

    public PromoCodeCache(IConnectionMultiplexer? redis, ILogger<PromoCodeCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<PromoCodeBindingRow?> TryGetAsync(string tenantId, string code, CancellationToken ct)
    {
        if (_redis is null) return null;
        try
        {
            var raw = await _redis.GetDatabase().StringGetAsync(Key(tenantId, code)).WaitAsync(ct);
            if (!raw.HasValue) return null;
            return JsonSerializer.Deserialize<PromoCodeBindingRow>(raw!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Promo code cache read failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    public async Task SetAsync(string tenantId, string code, PromoCodeBindingRow binding, CancellationToken ct)
    {
        if (_redis is null) return;
        try
        {
            var json = JsonSerializer.Serialize(binding);
            await _redis.GetDatabase().StringSetAsync(Key(tenantId, code), json, Ttl).WaitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Promo code cache write failed for tenant {TenantId}", tenantId);
        }
    }

    private static string Key(string tenantId, string code) =>
        $"promo:code:{tenantId}:{code.ToUpperInvariant()}";
}
