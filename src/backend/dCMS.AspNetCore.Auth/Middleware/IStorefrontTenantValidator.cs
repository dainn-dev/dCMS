namespace dCMS.AspNetCore.Auth.Middleware;

/// <summary>
/// DAI-751 / US-4: Validates the <c>X-Active-Tenant</c> header against the set of
/// active branches under the current client. Implementations are expected to cache
/// the allowed-tenants set to avoid hot-path DB lookups under 2000 CCU.
/// </summary>
public interface IStorefrontTenantValidator
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="tenantId"/> is an active branch under the
    /// configured client. Implementations should be safe to call concurrently.
    /// </summary>
    Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken = default);
}
