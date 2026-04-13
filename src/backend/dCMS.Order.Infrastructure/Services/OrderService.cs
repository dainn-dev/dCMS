using dCMS.Core.Messaging;
using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Infrastructure.Services;

public sealed class OrderService : IOrderService
{
    private readonly string _connectionString;
    private readonly OrderQueryStore _queryStore;
    private readonly IInventoryClient _inventoryClient;
    private readonly IBus _bus;
    private readonly IOrderDetailCache _orderDetailCache;

    public OrderService(
        IConfiguration configuration,
        OrderQueryStore queryStore,
        IInventoryClient inventoryClient,
        IBus bus,
        IOrderDetailCache orderDetailCache)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _queryStore = queryStore;
        _inventoryClient = inventoryClient;
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _orderDetailCache = orderDetailCache ?? throw new ArgumentNullException(nameof(orderDetailCache));
    }

    public Task<Core.Domain.Order?> GetByIdAsync(string tenantId, string storeId, string orderId, CancellationToken cancellationToken = default) =>
        _queryStore.GetByIdAsync(tenantId, storeId, orderId, cancellationToken);

    public Task<TimedOrder?> GetTimedByIdAsync(string tenantId, string storeId, string orderId, CancellationToken cancellationToken = default) =>
        _queryStore.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken);

    public Task<OrderListPage> ListOrdersAsync(OrderListQuery query, CancellationToken cancellationToken = default) =>
        _queryStore.ListOrdersAsync(query, cancellationToken);

    public Task<Core.Domain.Order?> GetByIdempotencyKeyAsync(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        _queryStore.GetByIdempotencyKeyAsync(tenantId, storeId, idempotencyKey, cancellationToken);

    public async Task<CreateOrderResult> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(command));

        var existing = await _queryStore
            .GetByIdempotencyKeyAsync(command.TenantId, command.StoreId, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return new CreateOrderResult(existing, PaymentUrl: null, IsIdempotentReplay: true);

        var stockLines = command.Lines
            .Select(l => new InventoryCheckLine(l.VariantId, l.WarehouseId, l.Quantity))
            .ToList();
        await _inventoryClient
            .EnsureStockAvailableAsync(command.TenantId, command.StoreId, stockLines, cancellationToken)
            .ConfigureAwait(false);

        var items = command.Lines.Select(l => new Core.Domain.OrderItem(
            l.LineId,
            l.ProductId,
            l.VariantId,
            l.Quantity,
            l.UnitPrice,
            l.ProductNameSnapshot,
            l.VariantSnapshotJson)).ToList();

        var placementLines = command.Lines
            .Select(l => new Core.Domain.OrderPlacedLine(l.VariantId, l.WarehouseId, l.Quantity))
            .ToList();

        var order = Core.Domain.Order.Create(
            command.OrderId,
            command.TenantId,
            command.StoreId,
            command.CustomerId,
            items,
            placementLines,
            command.ShippingAddress,
            command.OccurredAt);

        var events = order.DomainEvents.ToArray();

        await using var uow = new OrderUnitOfWork(_connectionString);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await uow.SaveOrderAsync(order, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            await uow.AppendOutboxAsync(events, cancellationToken).ConfigureAwait(false);
            order.ClearDomainEvents();
            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Payment URL comes from Payment Service after saga emits ProcessPaymentV1 (US-19).
        return new CreateOrderResult(order, PaymentUrl: null, IsIdempotentReplay: false);
    }

    public async Task<CancelOrderResult> CancelOrderAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(command));

        var timed = await GetTimedByIdAsync(command.TenantId, command.StoreId, command.OrderId, cancellationToken)
            .ConfigureAwait(false);
        if (timed is null)
            return new CancelOrderResult.NotFound();

        var order = timed.Order;
        if (!string.IsNullOrEmpty(command.CallerCustomerId) &&
            !string.Equals(order.CustomerId, command.CallerCustomerId, StringComparison.Ordinal))
            return new CancelOrderResult.Forbidden();

        if (order.Status == Core.Domain.OrderStatus.Cancelled)
            return new CancelOrderResult.AlreadyCancelled(order);

        if (order.Status is Core.Domain.OrderStatus.Shipped or Core.Domain.OrderStatus.Delivered)
            return new CancelOrderResult.NotCancellable("Cannot cancel a shipped or delivered order.");

        await using var uow = new OrderUnitOfWork(_connectionString);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        ManualOrderCancelOutcome outcome;
        try
        {
            outcome = await uow
                .TryMarkOrderCancelledFromApiAsync(
                    command.TenantId,
                    command.StoreId,
                    command.OrderId,
                    command.Reason,
                    command.OccurredAt,
                    cancellationToken)
                .ConfigureAwait(false);

            if (outcome == ManualOrderCancelOutcome.Success)
                await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            else
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        switch (outcome)
        {
            case ManualOrderCancelOutcome.NotFound:
                return new CancelOrderResult.NotFound();
            case ManualOrderCancelOutcome.AlreadyCancelled:
                {
                    var reloaded = await GetByIdAsync(command.TenantId, command.StoreId, command.OrderId, cancellationToken)
                        .ConfigureAwait(false);
                    return new CancelOrderResult.AlreadyCancelled(reloaded ?? order);
                }
            case ManualOrderCancelOutcome.NotCancellable:
                return new CancelOrderResult.NotCancellable(
                    "Order cannot be cancelled in the current state.");
            case ManualOrderCancelOutcome.Success:
                await _orderDetailCache.InvalidateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
                await _bus
                    .Publish(
                        new OrderCustomerCancellationV1(
                            command.OrderId,
                            command.TenantId,
                            command.StoreId,
                            command.Reason,
                            command.OccurredAt),
                        cancellationToken)
                    .ConfigureAwait(false);
                var cancelled = await GetByIdAsync(command.TenantId, command.StoreId, command.OrderId, cancellationToken)
                    .ConfigureAwait(false);
                return new CancelOrderResult.Ok(cancelled ?? order);
            default:
                throw new InvalidOperationException($"Unexpected cancel outcome: {outcome}.");
        }
    }
}
