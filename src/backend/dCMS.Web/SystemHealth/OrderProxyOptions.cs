namespace dCMS.Web.SystemHealth;

public sealed class OrderProxyOptions
{
    public const string SectionName = "DCMS:OrderProxy";

    public string OrderApiBaseUrl { get; set; } = "http://localhost:5003";

    /// <summary>Placeholder scope for Order JWT (SuperAdmin bypasses tenant checks).</summary>
    public string DefaultTenantId { get; set; } = "00000000-0000-0000-0000-000000000001";

    public string DefaultStoreId { get; set; } = "00000000-0000-0000-0000-000000000002";
}
