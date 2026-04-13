namespace dCMS.Order.Core.Ordering;

/// <summary>Application service for order lifecycle (DAI-313).</summary>
public interface IOrderService
{
    Task<Domain.Order?> GetByIdAsync(string tenantId, string storeId, string orderId, CancellationToken cancellationToken = default);

    Task<Domain.Order?> GetByIdempotencyKeyAsync(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an order, calls Payment for an intent, persists with <see cref="Domain.OrderPlaced"/> outbox in one transaction.
    /// On idempotent replay (same idempotency key), returns the stored order and <c>PaymentUrl</c> is <see langword="null"/>.
    /// </summary>
    Task<CreateOrderResult> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
}
