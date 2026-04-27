using dCMS.Messaging.Contracts;

namespace dCMS.Core.Messaging;

/// <summary>Order saga orchestration contracts (US-19 / DAI-317). Versioned names align with order-service design.</summary>
public sealed record OrderPlacedLineV1(string VariantId, string WarehouseId, int Quantity);

/// <summary>
/// Sales-side line: product/quantity/line-total snapshot. Reports.Worker uses this to project sales-by-product
/// without cross-DB reads against dcms_order. Carried alongside <see cref="OrderPlacedLineV1"/> (saga side).
/// </summary>
public sealed record OrderPlacedItemV1(string ProductId, int Quantity, decimal LineTotal);

[MessageVersion("OrderPlaced.v1")]
public sealed record OrderPlacedV1(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderPlacedLineV1> Lines,
    DateTimeOffset OccurredAt,
    IReadOnlyList<OrderPlacedItemV1>? Items = null);

public sealed record ReserveStockLineV1(string VariantId, string WarehouseId, int Quantity);

public sealed record ReserveStockV1(
    Guid CorrelationId,
    string OrderId,
    string TenantId,
    string StoreId,
    IReadOnlyList<ReserveStockLineV1> Lines,
    DateTimeOffset TimeoutAt);

public sealed record ProcessPaymentV1(
    Guid CorrelationId,
    string OrderId,
    string TenantId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    DateTimeOffset TimeoutAt);

public sealed record ReleaseStockV1(
    Guid CorrelationId,
    string OrderId,
    string TenantId,
    string StoreId,
    IReadOnlyList<ReserveStockLineV1> Lines);

/// <summary>Saga schedule: no <see cref="StockReservedV1"/> / <see cref="StockReservationFailedV1"/> before deadline (DAI-318).</summary>
public sealed record StockReservationTimeoutV1(string OrderId);

/// <summary>Saga schedule: no <see cref="PaymentCompletedV1"/> / <see cref="PaymentFailedV1"/> before deadline (DAI-318).</summary>
public sealed record PaymentTimeoutV1(string OrderId);

/// <summary>
/// Customer/API requested cancellation after the read model was updated (US-21 / DAI-326). Saga aligns inventory
/// (<see cref="ReleaseStockV1"/> when stock was reserved); <see cref="OrderCancelledV1"/> is not re-published here.
/// </summary>
public sealed record OrderCustomerCancellationV1(
    string OrderId,
    string TenantId,
    string StoreId,
    string Reason,
    DateTimeOffset OccurredAt);

/// <summary>Emitted when saga reaches <c>Confirmed</c>; order service updates read model (US-19).</summary>
public sealed record OrderPaymentSettledV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>
/// Saga reached <c>Cancelled</c> (US-20 / DAI-321). Downstream notification/analytics; order service consumer
/// sets read model <c>Cancelled</c> and appends domain <c>OrderCancelled</c> outbox when still <c>PaymentPending</c>.
/// </summary>
public sealed record OrderCancelledV1(
    string OrderId,
    string TenantId,
    string StoreId,
    string Reason,
    DateTimeOffset OccurredAt);

/// <summary>Warehouse / OMS starts fulfillment after payment (external → saga <c>Processing</c>).</summary>
public sealed record OrderFulfillmentStartedV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>Carrier handoff (external → saga <c>Shipped</c>).</summary>
public sealed record OrderShippedForSagaV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>Proof of delivery (external → saga <c>Delivered</c>).</summary>
public sealed record OrderDeliveredForSagaV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>Internal: saga updates read model / outbox for a lifecycle phase.</summary>
public sealed record OrderStatusProjectionV1(
    string OrderId,
    string TenantId,
    string StoreId,
    string Status,
    DateTimeOffset OccurredAt);
