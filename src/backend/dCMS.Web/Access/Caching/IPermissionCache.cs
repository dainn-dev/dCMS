namespace dCMS.Web.Access.Caching;

public interface IPermissionCache
{
    Task<IReadOnlyList<string>?> TryGetGrantedActionsAsync(
        string tenantId,
        int userId,
        string rolesHash,
        string module,
        CancellationToken ct = default);

    Task SetGrantedActionsAsync(
        string tenantId,
        int userId,
        string rolesHash,
        string module,
        IReadOnlyList<string> actions,
        CancellationToken ct = default);

    /// <summary>
    /// Bumps a tenant-level permissions version so subsequent cache keys change.
    /// Used as "active invalidation" when role/module permissions are updated.
    /// </summary>
    Task BumpTenantVersionAsync(string tenantId, CancellationToken ct = default);
}

