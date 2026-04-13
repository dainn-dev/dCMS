namespace dCMS.Order.Core.Domain;

/// <summary>Line item with catalog snapshots at order time (US-18).</summary>
public sealed class OrderItem
{
    public OrderItem(
        string id,
        string productId,
        string variantId,
        int quantity,
        Money unitPrice,
        string productNameSnapshot,
        string variantSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Line id is required.", nameof(id));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("Product name snapshot is required.", nameof(productNameSnapshot));

        Id = id;
        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        ProductNameSnapshot = productNameSnapshot;
        VariantSnapshotJson = variantSnapshotJson ?? "{}";
    }

    public string Id { get; }
    public string ProductId { get; }
    public string VariantId { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }
    public string ProductNameSnapshot { get; }
    public string VariantSnapshotJson { get; }

    public Money LineTotal() => new Money(UnitPrice.Amount * Quantity, UnitPrice.Currency);
}
