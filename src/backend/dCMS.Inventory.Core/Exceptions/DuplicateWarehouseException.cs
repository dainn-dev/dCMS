namespace dCMS.Inventory.Exceptions;

public sealed class DuplicateWarehouseException : Exception
{
    public DuplicateWarehouseException(string warehouseId)
        : base($"Warehouse id '{warehouseId}' already exists for this store.") =>
        WarehouseId = warehouseId;

    public string WarehouseId { get; }
}
