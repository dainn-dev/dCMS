namespace dCMS.Core.Messaging;

// DAI-306 versioning: additive optional fields OK within the same v1 contract; breaking changes require a new versioned type/topic (e.g. *.v2).

/// <summary>Inventory outbox / Rabbit contract (serialized to Inventory OutboxEvents as <c>StockUpdated.v1</c>).</summary>
public sealed record StockUpdatedV1(
    string VariantId,
    string WarehouseId,
    string TenantId,
    string StoreId,
    int Quantity,
    int ReservedQuantity,
    DateTimeOffset OccurredAt);
