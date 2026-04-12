namespace dCMS.Core.Persistence;

public sealed record StoreCatalogSettingsRow(string TenantId, string StoreId, bool ApprovalRequired, int? LowStockThreshold,
    DateTimeOffset UpdatedAt);
