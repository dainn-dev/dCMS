namespace dCMS.AspNetCore.Auth;

/// <summary>Role names aligned with dCMS RBAC (subset for API US-5).</summary>
public static class DcmsRoles
{
    /// <summary>B2C / storefront user; <c>sub</c> aligns with order <c>CustomerId</c> (US-21 / DAI-328).</summary>
    public const string Customer = "Customer";

    public const string SuperAdmin = "SuperAdmin";
    public const string ChainAdmin = "ChainAdmin";
    public const string BrandManager = "BrandManager";
    public const string StoreManager = "StoreManager";
    public const string StoreStaff = "StoreStaff";
}
