namespace dCMS.AspNetCore.Auth;

/// <summary>Role names aligned with dCMS RBAC (subset for API US-5).</summary>
public static class DcmsRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ChainAdmin = "ChainAdmin";
    public const string BrandManager = "BrandManager";
    public const string StoreManager = "StoreManager";
    public const string StoreStaff = "StoreStaff";
}
