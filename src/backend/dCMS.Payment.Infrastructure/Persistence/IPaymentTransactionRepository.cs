using dCMS.Payment.Core;

namespace dCMS.Payment.Infrastructure.Persistence;

public interface IPaymentTransactionRepository
{
    Task InsertInitiatedAsync(PaymentTransactionInsert row, CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetLatestByOrderIdAsync(
        Guid orderId,
        Guid tenantId,
        string clientId,
        string provider,
        CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetLatestByPaymentIntentIdAsync(
        string paymentIntentId,
        Guid tenantId,
        string clientId,
        string provider,
        Guid? storeId = null,
        CancellationToken cancellationToken = default);

    Task UpdateStatusByIdAsync(
        Guid id,
        Guid tenantId,
        Guid storeId,
        string clientId,
        string provider,
        string status,
        CancellationToken cancellationToken = default);

    Task<bool> TryRecordWebhookDeliveryAsync(
        string provider,
        string eventId,
        string signatureDigest,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentTransactionInsert(
    Guid Id,
    Guid OrderId,
    Guid TenantId,
    Guid StoreId,
    string ClientId,
    string CustomerId,
    string PaymentMethod,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    string Provider);
