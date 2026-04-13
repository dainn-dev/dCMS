using dCMS.Payment.Core;

namespace dCMS.Payment.Infrastructure.Persistence;

public interface IPaymentTransactionRepository
{
    Task InsertInitiatedAsync(PaymentTransactionInsert row, CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetLatestByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetLatestByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default);

    Task UpdateStatusByIdAsync(Guid id, string status, CancellationToken cancellationToken = default);
}

public sealed record PaymentTransactionInsert(
    Guid Id,
    Guid OrderId,
    Guid TenantId,
    Guid StoreId,
    string CustomerId,
    string PaymentMethod,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    string Provider);
