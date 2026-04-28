namespace dCMS.Order.Core.Domain;

/// <summary>DAI-695 — pure rules deriving Order-level status from per-item fulfillment statuses.</summary>
public static class OrderStatusDerivation
{
    /// <summary>
    /// Derive Order.Status from a non-empty list of items.
    /// Pre-fulfillment states (PaymentPending/Confirmed/PaymentFailed/etc.) are NOT derived
    /// — they live above the item lifecycle and must be set by saga / explicit transitions.
    /// </summary>
    public static OrderStatus Derive(IReadOnlyList<OrderItem> items)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException("Items required for derivation.", nameof(items));

        var statuses = items.Select(i => i.FulfillmentStatus).ToArray();

        if (statuses.All(s => s == OrderItemFulfillmentStatus.Cancelled))
            return OrderStatus.Cancelled;
        if (statuses.All(s => s == OrderItemFulfillmentStatus.Returned))
            return OrderStatus.Returned;
        if (statuses.All(s => s == OrderItemFulfillmentStatus.Delivered))
            return OrderStatus.Delivered;
        if (statuses.All(s => s == OrderItemFulfillmentStatus.PickedUp))
            return OrderStatus.PickedUp;
        if (statuses.All(s => s == OrderItemFulfillmentStatus.Shipped))
            return OrderStatus.Shipped;
        if (statuses.All(s => s == OrderItemFulfillmentStatus.ReadyForDelivery))
            return OrderStatus.ReadyForDelivery;

        var distinct = statuses.Distinct().ToArray();
        var hasTerminalProgress = statuses.Any(s =>
            s is OrderItemFulfillmentStatus.Delivered
              or OrderItemFulfillmentStatus.Shipped
              or OrderItemFulfillmentStatus.PickedUp
              or OrderItemFulfillmentStatus.Returned
              or OrderItemFulfillmentStatus.Cancelled);

        if (hasTerminalProgress && distinct.Length > 1)
            return OrderStatus.PartialFulfilled;

        // All items in {Open, Allocated} or mixed pre-fulfillment — order is being processed.
        return OrderStatus.Processing;
    }
}
