namespace dCMS.Payment.Core;

public enum PaymentTransactionStatus
{
    Pending = 0,
    Initiated = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4,
}

/// <summary>Read model for <c>PaymentTransactions</c> (PostgreSQL).</summary>
public sealed record PaymentTransaction(
    Guid Id,
    Guid OrderId,
    Guid TenantId,
    Guid StoreId,
    string CustomerId,
    string PaymentMethod,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    string Provider,
    DateTimeOffset CreatedAt);
