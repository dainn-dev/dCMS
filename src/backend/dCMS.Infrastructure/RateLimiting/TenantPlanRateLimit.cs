using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace dCMS.Infrastructure.RateLimiting;

/// <summary>US-11: partition key + permit limit from Redis <c>dcms:tenant:plan:{tenantId}</c> (starter/pro/enterprise).</summary>
public sealed class TenantPlanRateLimit
{
    public const string RedisKeyPrefix = "dcms:tenant:plan:";

    public TenantPlanRateLimit(IConfiguration configuration, IConnectionMultiplexer? redis)
    {
        _defaultPermit = configuration.GetValue("RateLimiting:PermitLimit", 200);
        _windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);
        _redis = redis;
    }

    private readonly int _defaultPermit;
    private readonly int _windowSeconds;
    private readonly IConnectionMultiplexer? _redis;

    public string ResolvePartitionKey(HttpContext http)
    {
        var tenant = http.Request.RouteValues.TryGetValue("tenantId", out var tv) ? tv?.ToString()?.Trim() : null;
        if (string.IsNullOrEmpty(tenant))
            return $"anon:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        var tier = ResolveTierSync(tenant);
        return $"{tenant}:{tier}";
    }

    public int ResolvePermitLimit(string partitionKey)
    {
        var tier = partitionKey.Contains(':', StringComparison.Ordinal)
            ? partitionKey.Split(':', 2)[1]
            : "starter";
        return tier.ToLowerInvariant() switch
        {
            "enterprise" => 1000,
            "pro" => 500,
            _ => _defaultPermit
        };
    }

    public TimeSpan Window => TimeSpan.FromSeconds(_windowSeconds);

    private string ResolveTierSync(string tenantId)
    {
        if (_redis is null)
            return "starter";
        try
        {
            var v = _redis.GetDatabase().StringGet(RedisKeyPrefix + tenantId);
            if (!v.HasValue)
                return "starter";
            return v.ToString().Trim().ToLowerInvariant();
        }
        catch
        {
            return "starter";
        }
    }
}
