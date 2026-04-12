using dCMS.Core.Exceptions;

namespace dCMS.Inventory.Models;

public sealed class VariantStock
{
    public int Id { get; init; }
    public string VariantId { get; init; } = null!;
    public string WarehouseId { get; init; } = null!;
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    /// <summary>Optimistic concurrency token (PostgreSQL <c>Revision</c> column).</summary>
    public long RowVersion { get; set; }

    public int AvailableQuantity => Quantity - ReservedQuantity;

    public void Reserve(int qty)
    {
        if (qty > AvailableQuantity)
            throw new OutOfStockException(VariantId, qty, AvailableQuantity);
        ReservedQuantity += qty;
    }

    public void Release(int qty) => ReservedQuantity = Math.Max(0, ReservedQuantity - qty);

    public void Adjust(int delta)
    {
        var newQuantity = Quantity + delta;
        if (newQuantity < ReservedQuantity)
            throw new StockInvariantException(
                $"Cannot adjust stock for {VariantId}: resulting quantity {newQuantity} would be less than reserved {ReservedQuantity}.");
        if (newQuantity < 0)
            throw new StockInvariantException(
                $"Cannot adjust stock for {VariantId}: resulting quantity {newQuantity} would be negative.");
        Quantity = newQuantity;
    }

    public static VariantStock Restore(int id, string variantId, string warehouseId, int quantity, int reservedQuantity,
        long rowVersion) =>
        new()
        {
            Id = id,
            VariantId = variantId,
            WarehouseId = warehouseId,
            Quantity = quantity,
            ReservedQuantity = reservedQuantity,
            RowVersion = rowVersion
        };
}
