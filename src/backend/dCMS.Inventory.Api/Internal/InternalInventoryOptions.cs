namespace dCMS.Inventory.Api.Internal;

public sealed class InternalInventoryOptions
{
    public const string SectionName = "InternalInventory";

    /// <summary>When non-empty, <c>/internal/inventory/*</c> is enabled and requires header <c>X-Internal-Api-Key</c> matching this value (SHA-256 constant-time compare).</summary>
    public string ApiKey { get; set; } = "";
}
