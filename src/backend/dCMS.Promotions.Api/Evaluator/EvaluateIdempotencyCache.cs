using System.Text.Json;
using dCMS.Promotions.Contracts.Evaluate;
using StackExchange.Redis;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Redis-backed idempotency cache for evaluate responses keyed by (tenantId, idempotencyKey).
/// Returns null for both cache miss and Redis unavailability — callers re-evaluate on miss.
/// </summary>
public sealed class EvaluateIdempotencyCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<EvaluateIdempotencyCache> _logger;

    public EvaluateIdempotencyCache(IConnectionMultiplexer? redis, ILogger<EvaluateIdempotencyCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<EvaluateResponse?> TryGetAsync(string tenantId, string idempotencyKey, CancellationToken ct)
    {
        if (_redis is null || string.IsNullOrWhiteSpace(idempotencyKey)) return null;
        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(Key(tenantId, idempotencyKey)).WaitAsync(ct);
            if (!raw.HasValue) return null;
            return JsonSerializer.Deserialize<EvaluateResponse>(raw!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis idempotency lookup failed; treating as miss");
            return null;
        }
    }

    public async Task SetAsync(string tenantId, string idempotencyKey, EvaluateResponse response, CancellationToken ct)
    {
        if (_redis is null || string.IsNullOrWhiteSpace(idempotencyKey)) return;
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(response);
            await db.StringSetAsync(Key(tenantId, idempotencyKey), json, Ttl).WaitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis idempotency set failed; continuing without cache");
        }
    }

    private static string Key(string tenantId, string idempotencyKey) =>
        $"promo:evaluate:{tenantId}:{idempotencyKey}";
}
