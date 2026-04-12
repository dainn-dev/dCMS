using dCMS.Inventory.Models;

namespace dCMS.Inventory.Commands;

public sealed record AdjustStockCommand(
    string TenantId,
    string StoreId,
    string VariantId,
    string WarehouseId,
    int Delta,
    string CreatedBy,
    string? ReferenceId,
    StockMovementType MovementType = StockMovementType.Adjustment);

public sealed record ReserveStockCommand(
    string TenantId,
    string StoreId,
    string VariantId,
    string WarehouseId,
    int Quantity,
    string CreatedBy,
    string? ReferenceId);

public sealed record ReleaseStockCommand(
    string TenantId,
    string StoreId,
    string VariantId,
    string WarehouseId,
    int Quantity,
    string CreatedBy,
    string? ReferenceId);
