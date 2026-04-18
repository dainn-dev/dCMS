namespace dCMS.Web.Access.Services;

/// <summary>Checks module/action permissions for a given Umbraco backoffice user,
/// based on the user's UserGroup aliases mapped in <c>role_module_permissions</c>.</summary>
public interface IPermissionService
{
    /// <summary>Returns <c>true</c> if the user has at least one UserGroup that grants
    /// <paramref name="action"/> on <paramref name="module"/>.</summary>
    Task<bool> HasPermissionAsync(int userId, string module, string action, CancellationToken ct = default);

    /// <summary>Returns all actions granted to the user for the given <paramref name="module"/>.</summary>
    Task<IReadOnlyList<string>> GetGrantedActionsAsync(int userId, string module, CancellationToken ct = default);
}
