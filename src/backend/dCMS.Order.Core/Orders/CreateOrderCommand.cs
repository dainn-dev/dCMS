namespace dCMS.Order.Core.Ordering;

public sealed record CreateOrderLine(
    string LineId,
    string ProductId,
    string VariantId,
    /// <summary>Fulfilment warehouse for inventory sync check (DAI-314 / US-18).</summary>
    string WarehouseId,
    int Quantity,
    Domain.Money UnitPrice,
    string ProductNameSnapshot,
    string VariantSnapshotJson);

public sealed record CreateOrderCommand(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    string IdempotencyKey,
    IReadOnlyList<CreateOrderLine> Lines,
    Domain.ShippingAddress ShippingAddress,
    DateTimeOffset OccurredAt);
