namespace dCMS.Order.Core.Domain;

/// <summary>Line item with catalog snapshots at order time (US-18). DAI-694: per-item FulfillmentStatus.</summary>
public sealed class OrderItem
{
    public OrderItem(
        string id,
        string productId,
        string variantId,
        int quantity,
        Money unitPrice,
        string productNameSnapshot,
        string variantSnapshotJson,
        OrderItemFulfillmentStatus fulfillmentStatus = OrderItemFulfillmentStatus.Open,
        int returnedQuantity = 0,
        string? pickupPinHash = null,
        DateTimeOffset? pickedUpAt = null,
        string? pickedUpBy = null,
        decimal lineDiscount = 0m)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Line id is required.", nameof(id));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("Product name snapshot is required.", nameof(productNameSnapshot));
        if (returnedQuantity < 0 || returnedQuantity > quantity)
            throw new ArgumentOutOfRangeException(nameof(returnedQuantity), "ReturnedQuantity must be between 0 and Quantity.");
        if (lineDiscount < 0m)
            throw new ArgumentOutOfRangeException(nameof(lineDiscount), "LineDiscount cannot be negative.");
        var grossLineTotal = unitPrice.Amount * quantity;
        if (lineDiscount > grossLineTotal)
            throw new ArgumentOutOfRangeException(nameof(lineDiscount), "LineDiscount cannot exceed UnitPrice * Quantity.");

        Id = id;
        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        ProductNameSnapshot = productNameSnapshot;
        VariantSnapshotJson = variantSnapshotJson ?? "{}";
        FulfillmentStatus = fulfillmentStatus;
        ReturnedQuantity = returnedQuantity;
        PickupPinHash = pickupPinHash;
        PickedUpAt = pickedUpAt;
        PickedUpBy = pickedUpBy;
        LineDiscount = lineDiscount;
    }

    public string Id { get; }
    public string ProductId { get; }
    public string VariantId { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }
    public string ProductNameSnapshot { get; }
    public string VariantSnapshotJson { get; }

    public OrderItemFulfillmentStatus FulfillmentStatus { get; private set; }

    /// <summary>DAI-697 — units returned via RMA. When equal to <see cref="Quantity"/>, status moves to <see cref="OrderItemFulfillmentStatus.Returned"/>.</summary>
    public int ReturnedQuantity { get; private set; }

    /// <summary>DAI-696 — PBKDF2 hash of the customer-facing pickup PIN. Null until allocated for pickup.</summary>
    public string? PickupPinHash { get; private set; }
    public DateTimeOffset? PickedUpAt { get; private set; }
    public string? PickedUpBy { get; private set; }

    /// <summary>DAI-725 — sum of promotion adjustments applied to this line at order create time.</summary>
    public decimal LineDiscount { get; }

    public Money GrossLineTotal() => new(UnitPrice.Amount * Quantity, UnitPrice.Currency);

    public Money LineTotal()
    {
        var net = (UnitPrice.Amount * Quantity) - LineDiscount;
        if (net < 0m) net = 0m;
        return new Money(net, UnitPrice.Currency);
    }

    /// <summary>DAI-694 — allowed transitions table (mirrors backoffice <c>NEXT_STATUSES</c> guard).</summary>
    private static readonly Dictionary<OrderItemFulfillmentStatus, OrderItemFulfillmentStatus[]> Transitions = new()
    {
        [OrderItemFulfillmentStatus.Open]             = [OrderItemFulfillmentStatus.Allocated, OrderItemFulfillmentStatus.Cancelled],
        [OrderItemFulfillmentStatus.Allocated]        = [OrderItemFulfillmentStatus.ReadyForDelivery, OrderItemFulfillmentStatus.Cancelled],
        [OrderItemFulfillmentStatus.ReadyForDelivery] = [OrderItemFulfillmentStatus.Shipped, OrderItemFulfillmentStatus.PickedUp, OrderItemFulfillmentStatus.Cancelled],
        [OrderItemFulfillmentStatus.Shipped]          = [OrderItemFulfillmentStatus.Delivered],
        [OrderItemFulfillmentStatus.Delivered]        = [OrderItemFulfillmentStatus.Returned],
        [OrderItemFulfillmentStatus.PickedUp]         = [OrderItemFulfillmentStatus.Returned],
        [OrderItemFulfillmentStatus.Returned]         = [],
        [OrderItemFulfillmentStatus.Cancelled]        = [],
    };

    public static bool IsValidTransition(OrderItemFulfillmentStatus from, OrderItemFulfillmentStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && Array.IndexOf(allowed, to) >= 0;

    public void TransitionTo(OrderItemFulfillmentStatus next)
    {
        if (!IsValidTransition(FulfillmentStatus, next))
            throw new InvalidOperationException(
                $"Invalid item transition: {FulfillmentStatus} → {next} for line {Id}.");
        FulfillmentStatus = next;
    }

    /// <summary>DAI-696 — store PIN hash when item moves to <see cref="OrderItemFulfillmentStatus.ReadyForDelivery"/>.</summary>
    public void AssignPickupPin(string pinHash)
    {
        if (string.IsNullOrWhiteSpace(pinHash))
            throw new ArgumentException("PIN hash is required.", nameof(pinHash));
        PickupPinHash = pinHash;
    }

    /// <summary>DAI-696 — record pickup event after PIN verification.</summary>
    public void MarkPickedUp(string staffOrCustomerId, DateTimeOffset occurredAt)
    {
        TransitionTo(OrderItemFulfillmentStatus.PickedUp);
        PickedUpAt = occurredAt;
        PickedUpBy = string.IsNullOrWhiteSpace(staffOrCustomerId) ? null : staffOrCustomerId.Trim();
    }

    /// <summary>DAI-697 — record returned units. When all units are returned, status flips to <see cref="OrderItemFulfillmentStatus.Returned"/>.</summary>
    public void RecordReturn(int qty)
    {
        if (qty <= 0)
            throw new ArgumentOutOfRangeException(nameof(qty), "Return qty must be positive.");
        if (FulfillmentStatus is not (OrderItemFulfillmentStatus.Delivered or OrderItemFulfillmentStatus.PickedUp))
            throw new InvalidOperationException(
                $"Cannot return line {Id}: not in Delivered/PickedUp state (current: {FulfillmentStatus}).");
        if (ReturnedQuantity + qty > Quantity)
            throw new InvalidOperationException(
                $"Cannot return {qty} units of line {Id}: only {Quantity - ReturnedQuantity} units remain returnable.");

        ReturnedQuantity += qty;
        if (ReturnedQuantity == Quantity)
            FulfillmentStatus = OrderItemFulfillmentStatus.Returned;
    }
}
