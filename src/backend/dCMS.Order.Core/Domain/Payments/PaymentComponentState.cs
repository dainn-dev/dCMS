namespace dCMS.Order.Core.Domain.Payments;

public enum PaymentComponentState
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5,
}
