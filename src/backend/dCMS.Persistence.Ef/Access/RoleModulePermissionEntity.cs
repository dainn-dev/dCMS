namespace dCMS.Persistence.Ef.Access;

/// <summary>Maps a Umbraco UserGroup alias to a granted module/action permission.
/// Table: <c>role_module_permissions</c>.</summary>
public sealed class RoleModulePermissionEntity
{
    /// <summary>Umbraco <c>IUserGroup.Alias</c> value.</summary>
    public string RoleAlias { get; set; } = null!;

    /// <summary>Module identifier, e.g. <c>products</c>, <c>orders</c>, <c>access</c>.</summary>
    public string Module { get; set; } = null!;

    /// <summary>Action identifier, e.g. <c>read</c>, <c>write</c>, <c>delete</c>, <c>export</c>.</summary>
    public string Action { get; set; } = null!;

    /// <summary><c>true</c> = permission granted; <c>false</c> = explicitly denied.</summary>
    public bool Granted { get; set; }
}
