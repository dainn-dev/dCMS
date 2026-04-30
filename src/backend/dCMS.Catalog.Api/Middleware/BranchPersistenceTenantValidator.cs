using System.Collections.Concurrent;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Core.Persistence;

namespace dCMS.Catalog.Api.Middleware;

/// <summary>
/// DAI-751 / US-4: Adapter that satisfies <see cref="IStorefrontTenantValidator"/> by
/// reading active branches under the configured client from <see cref="IBranchPersistence"/>.
/// 30-second in-memory cache keyed by client id keeps the storefront hot path off the DB
/// under 2000 CCU.
/// </summary>
public sealed class BranchPersistenceTenantValidator : IStorefrontTenantValidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<string, CachedTenants> Cache = new(StringComparer.Ordinal);

    private readonly IBranchPersistence _branches;
    private readonly string _clientId;

    public BranchPersistenceTenantValidator(IBranchPersistence branches, IConfiguration configuration)
    {
        _branches = branches;
        var configured = configuration.GetSection("Dcms:Client")["Id"]?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Dcms:Client.Id is required for storefront tenant binding.");
        _clientId = configured;
    }

    public async Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var allowed = await GetAllowedTenantsAsync(cancellationToken).ConfigureAwait(false);
        return allowed.Contains(tenantId);
    }

    private async Task<HashSet<string>> GetAllowedTenantsAsync(CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(_clientId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Tenants;

        var rows = await _branches.ListActiveAsync(_clientId, cancellationToken).ConfigureAwait(false);
        var set = new HashSet<string>(rows.Select(b => b.TenantId), StringComparer.Ordinal);
        Cache[_clientId] = new CachedTenants(set, DateTimeOffset.UtcNow.Add(CacheTtl));
        return set;
    }

    private sealed record CachedTenants(HashSet<string> Tenants, DateTimeOffset ExpiresAt);
}
