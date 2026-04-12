namespace dCMS.Web.InventoryProxy;

public sealed class InventoryProxyOptions
{
    public const string SectionName = "DCMS:InventoryProxy";

    /// <summary>Inventory.Api base URL (no trailing slash), e.g. <c>http://localhost:5002</c>.</summary>
    public string InventoryApiBaseUrl { get; set; } = "http://localhost:5002";

    public string DefaultTenantId { get; set; } = "";

    public string DefaultStoreId { get; set; } = "";
}
