namespace dCMS.Order.Core.Domain;

public enum OrderFailureStatus
{
    PaymentFailed,
    AddressError,
    AuthFailed,
    StockError,
    SystemError
}

