using StackExchange.Redis;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// Redis-backed implementation of <see cref="IJwtBlacklist"/>. Uses key <c>jwt:blk:{jti}</c>
/// with TTL set to the JWT's <c>exp</c> so blacklist entries auto-evict.
/// Shared across services so an impersonation token revoked in Identity.Api is rejected
/// by every downstream API that runs <see cref="Middleware.ImpersonationAuditMiddleware"/>.
/// </summary>
public sealed class RedisJwtBlacklist : IJwtBlacklist
{
    private const string KeyPrefix = "jwt:blk:";
    private readonly IConnectionMultiplexer _redis;

    public RedisJwtBlacklist(IConnectionMultiplexer redis) => _redis = redis;

    public async Task BlacklistAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return;
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        var db = _redis.GetDatabase();
        await db.StringSetAsync(KeyPrefix + jti, "1", ttl).ConfigureAwait(false);
    }

    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync(KeyPrefix + jti).ConfigureAwait(false);
    }
}
