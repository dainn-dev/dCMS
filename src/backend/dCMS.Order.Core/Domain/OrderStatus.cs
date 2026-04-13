namespace dCMS.Order.Core.Domain;

public enum OrderStatus
{
    /// <summary>Order persisted; awaiting payment (US-18).</summary>
    PaymentPending = 0,

    Confirmed,
    Shipped,
    Cancelled,
}
