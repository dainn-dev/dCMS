using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>PostgreSQL-backed fulfillment configuration (DAI-612).</summary>
public interface IFulfillmentPersistence
{
    Task<(IReadOnlyList<FulfillmentGroupingRow> Items, int Total)> ListGroupingsAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<FulfillmentGroupingRow?> GetGroupingAsync(string id, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> GroupingCodeExistsAsync(string tenantId, string code, string? exceptId,
        CancellationToken cancellationToken = default);

    Task CreateGroupingAsync(FulfillmentGroupingRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateGroupingAsync(FulfillmentGroupingRow row, CancellationToken cancellationToken = default);

    Task<bool> DeleteGroupingAsync(string id, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FulfillmentSlotRow>> ListSlotsForGroupingAsync(string tenantId, string groupingId,
        CancellationToken cancellationToken = default);

    Task<FulfillmentSlotRow?> GetSlotAsync(string id, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> SlotCodeExistsAsync(string tenantId, string groupingId, string code, string? exceptId,
        CancellationToken cancellationToken = default);

    Task CreateSlotAsync(FulfillmentSlotRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateSlotAsync(FulfillmentSlotRow row, CancellationToken cancellationToken = default);

    Task<bool> DeleteSlotAsync(string id, string tenantId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CollectionLocationRow> Items, int Total)> ListCollectionLocationsAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CollectionLocationRow?> GetCollectionLocationAsync(string id, string tenantId,
        CancellationToken cancellationToken = default);

    Task CreateCollectionLocationAsync(CollectionLocationRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateCollectionLocationAsync(CollectionLocationRow row, CancellationToken cancellationToken = default);

    Task<bool> DeleteCollectionLocationAsync(string id, string tenantId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LogisticPartnerRow> Items, int Total)> ListLogisticPartnersAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<LogisticPartnerRow?> GetLogisticPartnerAsync(string id, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> LogisticPartnerCodeExistsAsync(string tenantId, string code, string? exceptId,
        CancellationToken cancellationToken = default);

    Task CreateLogisticPartnerAsync(LogisticPartnerRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateLogisticPartnerAsync(LogisticPartnerRow row, CancellationToken cancellationToken = default);

    Task<bool> DeleteLogisticPartnerAsync(string id, string tenantId, CancellationToken cancellationToken = default);

    Task<FulfillmentTenantSettingsRow?> GetTenantSettingsAsync(string tenantId,
        CancellationToken cancellationToken = default);

    Task UpsertTenantSettingsAsync(FulfillmentTenantSettingsRow row, CancellationToken cancellationToken = default);
}
