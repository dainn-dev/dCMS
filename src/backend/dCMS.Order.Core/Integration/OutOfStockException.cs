namespace dCMS.Order.Core.Integration;

/// <summary>Raised when Inventory internal check reports insufficient stock (US-18 / DAI-314).</summary>
public sealed class OutOfStockException : Exception
{
    public OutOfStockException(string variantId, string warehouseId, int requested, int available)
        : base(
            $"Insufficient stock for variant '{variantId}' at warehouse '{warehouseId}': requested {requested}, available {available}.")
    {
        VariantId = variantId;
        WarehouseId = warehouseId;
        Requested = requested;
        Available = available;
    }

    public string VariantId { get; }
    public string WarehouseId { get; }
    public int Requested { get; }
    public int Available { get; }
}
