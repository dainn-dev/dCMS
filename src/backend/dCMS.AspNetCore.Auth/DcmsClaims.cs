namespace dCMS.AspNetCore.Auth;

public static class DcmsClaims
{
    public const string TenantId = "tenant_id";
    public const string StoreId = "store_id";

    /// <summary>Optional: comma-separated list of store IDs the user may operate on (tenant-scoped).</summary>
    public const string StoreIds = "store_ids";

    /// <summary>Optional: comma-separated list of brand IDs the user may operate on (tenant-scoped).</summary>
    public const string BrandIds = "brand_ids";
}
