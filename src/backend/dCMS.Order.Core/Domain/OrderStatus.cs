namespace dCMS.Order.Core.Domain;

public enum OrderStatus
{
    /// <summary>Order persisted; awaiting payment (US-18).</summary>
    PaymentPending = 0,

    Confirmed,

    /// <summary>Fulfillment / packing after payment (US-19 saga <c>Processing</c>).</summary>
    Processing,

    Shipped,

    /// <summary>Carrier proof of delivery (US-19 saga <c>Delivered</c>).</summary>
    Delivered,

    Cancelled,

    // ── Failure states (DAI-637) ────────────────────────────────────────────
    // Failure = Order at a sub-state of an existing lifecycle stage that needs
    // operator intervention (retry / resolve). Same Orders table; no separate
    // domain entity.

    /// <summary>Payment intent was declined by gateway. Recoverable via retry.</summary>
    PaymentFailed,

    /// <summary>3DS / SCA challenge timed out or failed. Recoverable via re-issue.</summary>
    AuthFailed,

    /// <summary>Shipping address validation failed. Recoverable with corrected address.</summary>
    AddressError,

    /// <summary>Inventory reservation could not be honoured. Recoverable when stock returns.</summary>
    StockError,

    /// <summary>Saga compensation / unhandled exception during state machine.</summary>
    SystemError,

    // ── Item-level fulfillment states (DAI-695) ─────────────────────────────
    // Order-level rollup derived from per-item FulfillmentStatus. Exposed when
    // every item shares the corresponding state, or when items are mixed
    // (PartialFulfilled).

    /// <summary>All items picked + packed; awaiting carrier handoff or pickup.</summary>
    ReadyForDelivery,

    /// <summary>All items picked up by customer at collection point.</summary>
    PickedUp,

    /// <summary>All items returned via RMA.</summary>
    Returned,

    /// <summary>Items split across terminal states (e.g. some Delivered, some Cancelled or Returned).</summary>
    PartialFulfilled,
}
