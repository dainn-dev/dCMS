namespace dCMS.Order.Core.Ordering;

/// <summary>Application service for order lifecycle (DAI-313).</summary>
public interface IOrderService
{
    Task<Domain.Order?> GetByIdAsync(string tenantId, string storeId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>Order read model with <c>CreatedAt</c> for <c>GET /api/orders/{id}</c> (US-21 / DAI-325).</summary>
    Task<TimedOrder?> GetTimedByIdAsync(string tenantId, string storeId, string orderId, CancellationToken cancellationToken = default);

    Task<Domain.Order?> GetByIdempotencyKeyAsync(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Keyset list for <c>GET /api/orders</c> (US-21 / DAI-325).</summary>
    Task<OrderListPage> ListOrdersAsync(OrderListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an order, persists with <see cref="Domain.OrderPlaced"/> outbox in one transaction; outbox relay publishes <c>OrderPlacedV1</c> to start the US-19 saga (reserve stock → pay).
    /// On idempotent replay (same idempotency key), returns the stored order and <c>PaymentUrl</c> is <see langword="null"/>.
    /// </summary>
    Task<CreateOrderResult> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);

    /// <summary>US-21 / DAI-326 — cancel order in DB + outbox, then notify saga via <c>OrderCustomerCancellationV1</c>.</summary>
    Task<CancelOrderResult> CancelOrderAsync(CancelOrderCommand command, CancellationToken cancellationToken = default);
}
