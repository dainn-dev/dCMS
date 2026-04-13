namespace dCMS.Order.Core.Integration;

/// <summary>One line for <see cref="IInventoryClient.EnsureStockAvailableAsync"/> (maps to Inventory POST /internal/inventory/check).</summary>
public sealed record InventoryCheckLine(string VariantId, string WarehouseId, int Quantity);
