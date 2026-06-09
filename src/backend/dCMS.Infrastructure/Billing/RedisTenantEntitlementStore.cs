using System.Text.Json;
using dCMS.Billing.Domain;
using dCMS.Infrastructure.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Billing;

public sealed class RedisTenantEntitlementStore : ITenantEntitlementStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer? _redis;
    private readonly IMemoryCache _memory;
    private readonly TenantEntitlementCacheOptions _options;
    private readonly ILogger<RedisTenantEntitlementStore> _logger;

    public RedisTenantEntitlementStore(
        IConnectionMultiplexer? redis,
        IMemoryCache memory,
        IOptions<TenantEntitlementCacheOptions> options,
        ILogger<RedisTenantEntitlementStore> logger)
    {
        _redis = redis;
        _memory = memory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TenantEntitlementSnapshot?> TryGetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        tenantId = NormalizeTenantId(tenantId);
        var version = await GetTenantVersionAsync(tenantId).ConfigureAwait(false);
        var cacheKey = BuildPayloadKey(tenantId, version);

        if (_memory.TryGetValue(cacheKey, out TenantEntitlementSnapshot? cached) && cached is not null)
            return cached;

        if (_redis is null)
            return null;

        try
        {
            var raw = await _redis.GetDatabase().StringGetAsync(cacheKey).ConfigureAwait(false);
            if (!raw.HasValue)
                return null;

            var snapshot = Deserialize(raw.ToString());
            if (snapshot is null)
                return null;

            _memory.Set(cacheKey, snapshot, _options.MemoryTtl);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entitlement cache Redis read failed for tenant={TenantId}", tenantId);
            return null;
        }
    }

    public async Task PublishAsync(TenantEntitlementSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var tenantId = NormalizeTenantId(snapshot.TenantId);
        var version = snapshot.Version;
        var cacheKey = BuildPayloadKey(tenantId, version);

        _memory.Set(cacheKey, snapshot, _options.MemoryTtl);

        if (_redis is null)
            return;

        try
        {
            var db = _redis.GetDatabase();
            var payload = Serialize(snapshot);
            await db.StringSetAsync(cacheKey, payload, _options.RedisTtl).ConfigureAwait(false);
            await db.StringSetAsync(
                TenantPlanRateLimit.RedisKeyPrefix + tenantId,
                snapshot.PlanCode.ToPersistedValue(),
                _options.RedisTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entitlement cache Redis write failed for tenant={TenantId}", tenantId);
        }
    }

    public async Task<long> BumpVersionAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        tenantId = NormalizeTenantId(tenantId);
        _memory.Remove(VersionMemoryKey(tenantId));

        if (_redis is null)
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            var version = await _redis.GetDatabase().StringIncrementAsync(BuildVersionKey(tenantId)).ConfigureAwait(false);
            return (long)version;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entitlement cache version bump failed for tenant={TenantId}", tenantId);
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    private async Task<long> GetTenantVersionAsync(string tenantId)
    {
        var memKey = VersionMemoryKey(tenantId);
        if (_memory.TryGetValue(memKey, out long cached))
            return cached;

        long version = 0;
        if (_redis is not null)
        {
            try
            {
                var raw = await _redis.GetDatabase().StringGetAsync(BuildVersionKey(tenantId)).ConfigureAwait(false);
                if (raw.HasValue && long.TryParse(raw.ToString(), out var v))
                    version = v;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Entitlement cache version read failed for tenant={TenantId}", tenantId);
            }
        }

        _memory.Set(memKey, version, _options.VersionMemoryTtl);
        return version;
    }

    private string BuildPayloadKey(string tenantId, long version) =>
        $"{_options.KeyPrefix}:{tenantId}:v{version}";

    private string BuildVersionKey(string tenantId) =>
        $"{_options.KeyPrefix}:ver:{tenantId}";

    private string VersionMemoryKey(string tenantId) =>
        $"{_options.KeyPrefix}:vmem:{tenantId}";

    private static string NormalizeTenantId(string tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) ? "unknown" : tenantId.Trim();

    private static string Serialize(TenantEntitlementSnapshot snapshot) =>
        JsonSerializer.Serialize(new EntitlementCacheDto
        {
            TenantId = snapshot.TenantId,
            PlanCode = snapshot.PlanCode.ToPersistedValue(),
            SubscriptionState = snapshot.SubscriptionState.ToPersistedValue(),
            ManualInvoiceStatus = snapshot.ManualInvoiceStatus.ToPersistedValue(),
            TenantActive = snapshot.TenantActive,
            TrialEndsAt = snapshot.TrialEndsAt,
            MaxBrands = snapshot.MaxBrands,
            MaxActiveProducts = snapshot.MaxActiveProducts,
            Features = snapshot.Features.ToArray(),
            Version = snapshot.Version,
        }, Json);

    private static TenantEntitlementSnapshot? Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<EntitlementCacheDto>(json, Json);
        if (dto is null || string.IsNullOrWhiteSpace(dto.TenantId))
            return null;

        return TenantEntitlementSnapshot.Create(
            dto.TenantId,
            PlanCodeExtensions.ParsePersisted(dto.PlanCode),
            TenantSubscriptionStateExtensions.ParsePersisted(dto.SubscriptionState),
            ManualInvoiceStatusExtensions.ParsePersisted(dto.ManualInvoiceStatus),
            dto.TenantActive,
            dto.TrialEndsAt,
            dto.MaxBrands,
            dto.MaxActiveProducts,
            dto.Features,
            dto.Version);
    }

    private sealed class EntitlementCacheDto
    {
        public string TenantId { get; set; } = "";
        public string PlanCode { get; set; } = "starter";
        public string SubscriptionState { get; set; } = "trial";
        public string ManualInvoiceStatus { get; set; } = "none";
        public bool TenantActive { get; set; } = true;
        public DateTimeOffset? TrialEndsAt { get; set; }
        public int MaxBrands { get; set; }
        public int MaxActiveProducts { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
        public long Version { get; set; }
    }
}
