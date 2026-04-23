namespace dCMS.AspNetCore.Auth;

public static class DcmsPolicies
{
    public const string CatalogRead = "catalog:read";
    public const string CatalogWrite = "catalog:write";
    /// <summary>Approve / request-changes / reject on products in <c>pending_approval</c> (DAI-296).</summary>
    public const string CatalogApproval = "catalog:approval";
    public const string InventoryRead = "inventory:read";
    public const string InventoryWrite = "inventory:write";

    /// <summary>Order API (US-21): any authenticated principal; route handlers enforce customer vs staff scope.</summary>
    public const string OrderAccess = "order:access";

    /// <summary>Order outbox DLQ admin (US-F4 / DAI-362): SuperAdmin only.</summary>
    public const string OrderDlqAdmin = "order:dlq-admin";

    /// <summary>DAI-631: manage order failures (support / store operations).</summary>
    public const string OrderFailureManage = "order:failure-manage";
}
