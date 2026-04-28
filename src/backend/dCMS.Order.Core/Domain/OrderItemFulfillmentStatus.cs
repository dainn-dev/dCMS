namespace dCMS.Order.Core.Domain;

/// <summary>DAI-694 — per-item fulfillment lifecycle.</summary>
public enum OrderItemFulfillmentStatus
{
    /// <summary>Default after order creation, before stock allocation.</summary>
    Open = 0,

    /// <summary>Inventory reserved for this line.</summary>
    Allocated,

    /// <summary>Picked + packed; awaiting carrier handoff or customer pickup.</summary>
    ReadyForDelivery,

    /// <summary>Handed to carrier (in transit).</summary>
    Shipped,

    /// <summary>Carrier proof of delivery.</summary>
    Delivered,

    /// <summary>Customer picked up at store / collection point (DAI-696).</summary>
    PickedUp,

    /// <summary>Customer returned the item — RMA completed (DAI-697).</summary>
    Returned,

    /// <summary>Line was cancelled before fulfillment finished.</summary>
    Cancelled,
}
