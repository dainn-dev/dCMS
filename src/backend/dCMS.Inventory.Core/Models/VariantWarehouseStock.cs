namespace dCMS.Inventory.Models;

public sealed record VariantWarehouseStock(string WarehouseId, string WarehouseName, int Quantity, int ReservedQuantity)
{
    public int AvailableQuantity => Quantity - ReservedQuantity;
}
