namespace dCMS.AspNetCore.Auth;

public static class DcmsPolicies
{
    public const string CatalogRead = "catalog:read";
    public const string CatalogWrite = "catalog:write";
    /// <summary>Approve / request-changes / reject on products in <c>pending_approval</c> (DAI-296).</summary>
    public const string CatalogApproval = "catalog:approval";
    public const string InventoryRead = "inventory:read";
    public const string InventoryWrite = "inventory:write";
}
