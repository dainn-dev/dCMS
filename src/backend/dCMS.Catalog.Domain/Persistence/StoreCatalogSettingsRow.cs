namespace dCMS.Core.Persistence;

public sealed class StoreCatalogSettingsRow
{
    public string TenantId { get; init; } = null!;
    public string StoreId { get; init; } = null!;
    public bool ApprovalRequired { get; init; }
    public int? LowStockThreshold { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
