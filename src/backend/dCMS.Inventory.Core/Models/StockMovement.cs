namespace dCMS.Inventory.Models;

public sealed class StockMovement
{
    public long Id { get; init; }
    public string VariantId { get; init; } = null!;
    public string WarehouseId { get; init; } = null!;
    public int Delta { get; init; }
    public StockMovementType Type { get; init; }
    public string? ReferenceId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string CreatedBy { get; init; } = null!;

    public static StockMovement ForAppend(string variantId, string warehouseId, int delta, StockMovementType type,
        string createdBy, string? referenceId, DateTimeOffset createdAt) =>
        new()
        {
            Id = 0,
            VariantId = variantId,
            WarehouseId = warehouseId,
            Delta = delta,
            Type = type,
            ReferenceId = referenceId,
            CreatedAt = createdAt,
            CreatedBy = createdBy
        };
}
