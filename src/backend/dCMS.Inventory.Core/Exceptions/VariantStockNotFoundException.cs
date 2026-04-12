namespace dCMS.Inventory.Exceptions;

public sealed class VariantStockNotFoundException : Exception
{
    public VariantStockNotFoundException(string variantId, string warehouseId)
        : base($"No stock row for variant {variantId} in warehouse {warehouseId}.")
    {
        VariantId = variantId;
        WarehouseId = warehouseId;
    }

    public string VariantId { get; }
    public string WarehouseId { get; }
}
