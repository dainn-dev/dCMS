using dCMS.Core.Messaging;
using dCMS.Order.Infrastructure.Operations;
using MassTransit;

namespace dCMS.Order.Infrastructure.Sagas;

/// <summary>
/// Order lifecycle saga (US-19 / DAI-316). States: Placed → PaymentPending → Confirmed → Processing → Shipped → Delivered | Cancelled.
/// DAI-318: <see cref="StockReservationTimeoutV1"/> / <see cref="PaymentTimeoutV1"/>.
/// DAI-319: persist via PostgreSQL <c>OrderSagaState</c> (EF + <c>UsePostgres</c>).
/// DAI-321 (US-20): compensation publishes <see cref="ReleaseStockV1"/> when stock was reserved; all terminal cancellations publish <see cref="OrderCancelledV1"/>.
/// DAI-326 (US-21): <see cref="OrderCustomerCancellationV1"/> after API cancel — align saga + release stock when reserved.
/// US-F3 / DAI-356: late <see cref="PaymentCompletedV1"/> after cancel → <see cref="RefundPaymentV1"/> + <see cref="PaymentRefundedV1"/>.
/// </summary>
public sealed class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderPlaced, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => StockReserved, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => StockReservationFailed, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => StockReservationTimeout, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => PaymentCompleted, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => PaymentFailed, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => PaymentTimeout, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => FulfillmentStarted, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => ShippedForSaga, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => DeliveredForSaga, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => OrderCustomerCancellation, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));
        Event(() => PaymentRefunded, x => x.CorrelateById(ctx => Guid.Parse(ctx.Message.OrderId)));

        Initially(
            When(OrderPlaced)
                .Then(ctx =>
                {
                    var m = ctx.Message;
                    ctx.Saga.OrderId = m.OrderId;
                    ctx.Saga.TenantId = m.TenantId;
                    ctx.Saga.StoreId = m.StoreId;
                    ctx.Saga.CustomerId = m.CustomerId;
                    ctx.Saga.TotalAmount = m.TotalAmount;
                    ctx.Saga.Currency = m.Currency;
                    ctx.Saga.ReserveLines = m.Lines
                        .Select(l => new ReserveStockLineV1(l.VariantId, l.WarehouseId, l.Quantity))
                        .ToList();
                })
                .TransitionTo(Placed)
                .Publish(ctx => new ReserveStockV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Saga.ReserveLines,
                    DateTimeOffset.UtcNow.AddMinutes(30))));

        During(
            Placed,
            When(StockReserved)
                .TransitionTo(PaymentPending)
                .Publish(ctx => new ProcessPaymentV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.Currency,
                    PaymentMethod: "card",
                    DateTimeOffset.UtcNow.AddMinutes(15))),
            When(StockReservationFailed)
                .Publish(ctx => new OrderCancelledV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Message.Reason,
                    ctx.Message.OccurredAt))
                .TransitionTo(Cancelled),
            When(StockReservationTimeout)
                .Publish(ctx => new OrderCancelledV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    "stock_reservation_timeout",
                    DateTimeOffset.UtcNow))
                .TransitionTo(Cancelled),
            When(OrderCustomerCancellation)
                .TransitionTo(Cancelled),
            Ignore(PaymentTimeout));

        During(
            PaymentPending,
            When(PaymentFailed)
                .Publish(ctx => new ReleaseStockV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Saga.ReserveLines))
                .Publish(ctx => new OrderCancelledV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Message.Reason,
                    ctx.Message.OccurredAt))
                .TransitionTo(Cancelled),
            When(PaymentTimeout)
                .Publish(ctx => new ReleaseStockV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Saga.ReserveLines))
                .Publish(ctx => new OrderCancelledV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    "payment_timeout",
                    DateTimeOffset.UtcNow))
                // DAI-724: any partially-Authorized tender holds need to be released on timeout.
                .Publish(ctx => new ReleasePaymentComponentsV1(
                    Guid.TryParse(ctx.Saga.OrderId, out var oid) ? oid : Guid.Empty,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    Reason: "payment_timeout",
                    RequestedAt: DateTimeOffset.UtcNow))
                .TransitionTo(Cancelled),
            When(OrderCustomerCancellation)
                .Publish(ctx => new ReleaseStockV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Saga.ReserveLines))
                // DAI-724: release any in-flight Authorized tender holds.
                .Publish(ctx => new ReleasePaymentComponentsV1(
                    Guid.TryParse(ctx.Saga.OrderId, out var oid) ? oid : Guid.Empty,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    Reason: "customer_cancellation_during_payment",
                    RequestedAt: DateTimeOffset.UtcNow))
                .TransitionTo(Cancelled),
            Ignore(StockReservationTimeout));

        DuringAny(
            When(PaymentCompleted)
                .IfElse(
                    ctx => StateEquals(ctx.Saga, nameof(PaymentPending)),
                    payOk => payOk
                        .TransitionTo(Confirmed)
                        .Publish(ctx => new OrderPaymentSettledV1(
                            ctx.Saga.OrderId,
                            ctx.Saga.TenantId,
                            ctx.Saga.StoreId,
                            DateTimeOffset.UtcNow)),
                    lateOrDup => lateOrDup
                        .IfElse(
                            ctx => StateEquals(ctx.Saga, nameof(Cancelled)),
                            refund => refund
                                .Then(context =>
                                {
                                    if (!context.TryGetPayload<IServiceProvider>(out var sp))
                                        return;

                                    if (sp.GetService(typeof(IOperationAlerts)) is not IOperationAlerts alerts)
                                        return;

                                    _ = alerts.NotifyLatePaymentCompletedOnCancelledAsync(
                                        context.Saga.OrderId,
                                        context.Saga.TenantId,
                                        context.CancellationToken);
                                })
                                .Publish(s => new RefundPaymentV1(
                                    s.Saga.OrderId,
                                    s.Saga.TenantId,
                                    s.Saga.StoreId,
                                    s.Saga.TotalAmount,
                                    s.Saga.Currency,
                                    Reason: "late_payment_on_cancelled",
                                    RequestedAt: DateTimeOffset.UtcNow))
                                // DAI-724: also notify the multi-tender orchestrator so per-component
                                // Voucher/Loyalty holds are refunded/released idempotently.
                                .Publish(s => new ReleasePaymentComponentsV1(
                                    Guid.TryParse(s.Saga.OrderId, out var oid) ? oid : Guid.Empty,
                                    s.Saga.TenantId,
                                    s.Saga.StoreId,
                                    Reason: "late_payment_on_cancelled",
                                    RequestedAt: DateTimeOffset.UtcNow))
                                .TransitionTo(LatePaymentRefunding),
                            _ => _.Then(_ => { }))));

        During(
            Confirmed,
            When(FulfillmentStarted)
                .TransitionTo(Processing)
                .Publish(ctx => new OrderStatusProjectionV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    "Processing",
                    DateTimeOffset.UtcNow)),
            When(OrderCustomerCancellation)
                .Publish(ctx => new ReleaseStockV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Saga.ReserveLines))
                // DAI-724: payment components are already captured by this point — refund them.
                .Publish(ctx => new ReleasePaymentComponentsV1(
                    Guid.TryParse(ctx.Saga.OrderId, out var oid) ? oid : Guid.Empty,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    Reason: "customer_cancellation_after_confirm",
                    RequestedAt: DateTimeOffset.UtcNow))
                .TransitionTo(Cancelled),
            Ignore(StockReservationTimeout),
            Ignore(PaymentTimeout));

        During(
            Processing,
            When(ShippedForSaga)
                .TransitionTo(Shipped)
                .Publish(ctx => new OrderStatusProjectionV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    "Shipped",
                    DateTimeOffset.UtcNow)),
            When(OrderCustomerCancellation)
                .Publish(ctx => new ReleaseStockV1(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    ctx.Saga.ReserveLines))
                // DAI-724: payment components captured — refund them.
                .Publish(ctx => new ReleasePaymentComponentsV1(
                    Guid.TryParse(ctx.Saga.OrderId, out var oid) ? oid : Guid.Empty,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    Reason: "customer_cancellation_after_processing",
                    RequestedAt: DateTimeOffset.UtcNow))
                .TransitionTo(Cancelled),
            Ignore(StockReservationTimeout),
            Ignore(PaymentTimeout));

        During(
            Shipped,
            When(DeliveredForSaga)
                .TransitionTo(Delivered)
                .Publish(ctx => new OrderStatusProjectionV1(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.StoreId,
                    "Delivered",
                    DateTimeOffset.UtcNow)),
            Ignore(OrderCustomerCancellation),
            Ignore(StockReservationTimeout),
            Ignore(PaymentTimeout));

        During(
            Delivered,
            Ignore(OrderCustomerCancellation),
            Ignore(StockReservationTimeout),
            Ignore(PaymentTimeout));

        During(
            Cancelled,
            Ignore(OrderCustomerCancellation),
            Ignore(StockReservationTimeout),
            Ignore(PaymentTimeout),
            Ignore(StockReserved),
            Ignore(StockReservationFailed),
            Ignore(PaymentFailed),
            Ignore(FulfillmentStarted),
            Ignore(ShippedForSaga),
            Ignore(DeliveredForSaga));

        During(
            LatePaymentRefunding,
            When(PaymentRefunded)
                .TransitionTo(LatePaymentRefunded),
            Ignore(OrderPlaced),
            Ignore(StockReserved),
            Ignore(StockReservationFailed),
            Ignore(StockReservationTimeout),
            Ignore(PaymentCompleted),
            Ignore(PaymentFailed),
            Ignore(PaymentTimeout),
            Ignore(FulfillmentStarted),
            Ignore(ShippedForSaga),
            Ignore(DeliveredForSaga),
            Ignore(OrderCustomerCancellation));

        During(
            LatePaymentRefunded,
            Ignore(OrderPlaced),
            Ignore(StockReserved),
            Ignore(StockReservationFailed),
            Ignore(StockReservationTimeout),
            Ignore(PaymentCompleted),
            Ignore(PaymentFailed),
            Ignore(PaymentTimeout),
            Ignore(PaymentRefunded),
            Ignore(FulfillmentStarted),
            Ignore(ShippedForSaga),
            Ignore(DeliveredForSaga),
            Ignore(OrderCustomerCancellation));
    }

    public State Placed { get; private set; } = null!;
    public State PaymentPending { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Shipped { get; private set; } = null!;
    public State Delivered { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;
    public State LatePaymentRefunding { get; private set; } = null!;
    public State LatePaymentRefunded { get; private set; } = null!;

    public Event<OrderPlacedV1> OrderPlaced { get; private set; } = null!;
    public Event<StockReservedV1> StockReserved { get; private set; } = null!;
    public Event<StockReservationFailedV1> StockReservationFailed { get; private set; } = null!;
    public Event<StockReservationTimeoutV1> StockReservationTimeout { get; private set; } = null!;
    public Event<PaymentCompletedV1> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailedV1> PaymentFailed { get; private set; } = null!;
    public Event<PaymentTimeoutV1> PaymentTimeout { get; private set; } = null!;
    public Event<OrderFulfillmentStartedV1> FulfillmentStarted { get; private set; } = null!;
    public Event<OrderShippedForSagaV1> ShippedForSaga { get; private set; } = null!;
    public Event<OrderDeliveredForSagaV1> DeliveredForSaga { get; private set; } = null!;
    public Event<OrderCustomerCancellationV1> OrderCustomerCancellation { get; private set; } = null!;
    public Event<PaymentRefundedV1> PaymentRefunded { get; private set; } = null!;

    private static bool StateEquals(OrderSagaState saga, string stateName) =>
        string.Equals(saga.CurrentState?.Trim(), stateName, StringComparison.OrdinalIgnoreCase);
}
