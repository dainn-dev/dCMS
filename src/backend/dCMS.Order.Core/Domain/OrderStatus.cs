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
}
