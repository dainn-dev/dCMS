namespace dCMS.AspNetCore.Auth;

/// <summary>
/// DAI-752 (US-5) — invalidate an impersonation JWT before its <c>exp</c>.
/// Implementations are expected to be Redis-backed; an in-memory fallback is provided for tests.
/// </summary>
public interface IJwtBlacklist
{
    /// <summary>Mark a JWT as blacklisted until <paramref name="expiresAt"/>.</summary>
    Task BlacklistAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    /// <summary>True when the token's <c>jti</c> claim is blacklisted (or never if no entry).</summary>
    Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default);
}

/// <summary>In-memory fallback used by tests / local dev when Redis isn't configured.</summary>
public sealed class InMemoryJwtBlacklist : IJwtBlacklist
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _entries = new();

    public Task BlacklistAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        _entries[jti] = expiresAt;
        Sweep();
        return Task.CompletedTask;
    }

    public Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        Sweep();
        return Task.FromResult(_entries.ContainsKey(jti));
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _entries)
            if (kv.Value < now)
                _entries.TryRemove(kv.Key, out _);
    }
}
