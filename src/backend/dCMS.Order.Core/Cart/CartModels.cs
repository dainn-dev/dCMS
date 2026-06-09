namespace dCMS.Order.Core.Cart;

public sealed record CartLine(
    string LineId,
    string ProductId,
    string VariantId,
    string WarehouseId,
    int Quantity,
    decimal UnitPriceAmount,
    string Currency,
    string ProductNameSnapshot,
    string VariantSnapshotJson);

public sealed record CartSnapshot(
    string TenantId,
    string StoreId,
    string OwnerId,
    IReadOnlyList<CartLine> Lines,
    DateTimeOffset UpdatedAt);

public sealed record UpsertCartLineRequest(
    string? LineId,
    string ProductId,
    string VariantId,
    string WarehouseId,
    int Quantity,
    decimal UnitPriceAmount,
    string Currency,
    string ProductNameSnapshot,
    string VariantSnapshotJson);
