namespace dCMS.Web.CatalogProxy;

public sealed class CatalogProxyOptions
{
    public const string SectionName = "DCMS:CatalogProxy";

    /// <summary>Catalog.Api base URL (no trailing slash), e.g. <c>http://localhost:5001</c> or <c>http://catalog-api:5001</c> in Docker.</summary>
    public string CatalogApiBaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>Fallback tenant when the wizard UI does not send <c>tenantId</c> (dev only).</summary>
    public string DefaultTenantId { get; set; } = "";

    /// <summary>Fallback store when the wizard UI does not send <c>storeId</c> (dev only).</summary>
    public string DefaultStoreId { get; set; } = "";
}
