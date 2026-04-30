namespace dCMS.AspNetCore.Auth;

/// <summary>Role names aligned with dCMS RBAC (subset for API US-5).</summary>
public static class DcmsRoles
{
    /// <summary>B2C / storefront user; <c>sub</c> aligns with order <c>CustomerId</c> (US-21 / DAI-328).</summary>
    public const string Customer = "Customer";

    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// DAI-752 (US-5) — chain-wide HQ admin. JWT carries <c>client_id</c> only, no <c>tenant_id</c>.
    /// Read-only across all tenants under the client; must impersonate to perform writes.
    /// </summary>
    public const string ClientAdmin = "ClientAdmin";

    /// <summary>
    /// DAI-752 (US-5) — full write within a single tenant. JWT carries both <c>client_id</c>
    /// and <c>tenant_id</c>. Successor to ChainAdmin for tenant-scoped administration.
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Legacy tenant-wide admin role; kept for compatibility with pre-DAI-752 tokens.</summary>
    public const string ChainAdmin = "ChainAdmin";

    public const string BrandManager = "BrandManager";
    public const string StoreManager = "StoreManager";
    public const string StoreStaff = "StoreStaff";

    /// <summary>Operations/support role for order failures and customer issues (DAI-631).</summary>
    public const string CustomerSupport = "CustomerSupport";
}
