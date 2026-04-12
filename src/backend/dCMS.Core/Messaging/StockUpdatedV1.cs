namespace dCMS.Core.Messaging;

/// <summary>Inventory outbox / Rabbit contract (serialized to Inventory OutboxEvents as <c>StockUpdated.v1</c>).</summary>
public sealed record StockUpdatedV1(
    string VariantId,
    string WarehouseId,
    string TenantId,
    string StoreId,
    int Quantity,
    int ReservedQuantity,
    DateTimeOffset OccurredAt);
