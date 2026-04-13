using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Infrastructure.Messaging;

public sealed class OrderPaymentSettledConsumer : IConsumer<OrderPaymentSettledV1>
{
    private readonly string _connectionString;
    private readonly IOrderDetailCache _orderDetailCache;

    public OrderPaymentSettledConsumer(IConfiguration configuration, IOrderDetailCache orderDetailCache)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _orderDetailCache = orderDetailCache ?? throw new ArgumentNullException(nameof(orderDetailCache));
    }

    public async Task Consume(ConsumeContext<OrderPaymentSettledV1> context)
    {
        var m = context.Message;
        await using var uow = new OrderUnitOfWork(_connectionString);
        await uow.BeginAsync(context.CancellationToken).ConfigureAwait(false);
        try
        {
            await uow
                .ConfirmIfPaymentPendingAsync(m.TenantId, m.StoreId, m.OrderId, m.OccurredAt, context.CancellationToken)
                .ConfigureAwait(false);
            await uow.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            throw;
        }

        await _orderDetailCache.InvalidateAsync(m.OrderId, context.CancellationToken).ConfigureAwait(false);
    }
}

/// <summary>US-20 / DAI-321 — saga <c>OrderCancelledV1</c> → read model + outbox for relay.</summary>
public sealed class OrderCancelledIntegrationConsumer : IConsumer<OrderCancelledV1>
{
    private readonly string _connectionString;
    private readonly IOrderDetailCache _orderDetailCache;

    public OrderCancelledIntegrationConsumer(IConfiguration configuration, IOrderDetailCache orderDetailCache)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _orderDetailCache = orderDetailCache ?? throw new ArgumentNullException(nameof(orderDetailCache));
    }

    public async Task Consume(ConsumeContext<OrderCancelledV1> context)
    {
        var m = context.Message;
        await using var uow = new OrderUnitOfWork(_connectionString);
        await uow.BeginAsync(context.CancellationToken).ConfigureAwait(false);
        try
        {
            await uow
                .CancelIfPaymentPendingAsync(
                    m.TenantId,
                    m.StoreId,
                    m.OrderId,
                    m.Reason,
                    m.OccurredAt,
                    context.CancellationToken)
                .ConfigureAwait(false);
            await uow.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            throw;
        }

        await _orderDetailCache.InvalidateAsync(m.OrderId, context.CancellationToken).ConfigureAwait(false);
    }
}

public sealed class OrderStatusProjectionConsumer : IConsumer<OrderStatusProjectionV1>
{
    private readonly string _connectionString;
    private readonly IOrderDetailCache _orderDetailCache;

    public OrderStatusProjectionConsumer(IConfiguration configuration, IOrderDetailCache orderDetailCache)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _orderDetailCache = orderDetailCache ?? throw new ArgumentNullException(nameof(orderDetailCache));
    }

    public async Task Consume(ConsumeContext<OrderStatusProjectionV1> context)
    {
        var m = context.Message;
        var at = m.OccurredAt;

        var (expected, next, outbox) = m.Status switch
        {
            "Processing" => ("Confirmed", "Processing", (IReadOnlyList<IDomainEvent>?)null),
            "Shipped" => ("Processing", "Shipped", (IReadOnlyList<IDomainEvent>?)new List<IDomainEvent> { new OrderShipped(m.OrderId, at) }),
            "Delivered" => ("Shipped", "Delivered", (IReadOnlyList<IDomainEvent>?)new List<IDomainEvent> { new OrderDelivered(m.OrderId, at) }),
            _ => (null, null, null),
        };

        if (expected is null || next is null)
            return;

        await using var uow = new OrderUnitOfWork(_connectionString);
        await uow.BeginAsync(context.CancellationToken).ConfigureAwait(false);
        try
        {
            await uow
                .TrySetOrderStatusAsync(
                    m.TenantId,
                    m.StoreId,
                    m.OrderId,
                    expected,
                    next,
                    outbox,
                    at,
                    context.CancellationToken)
                .ConfigureAwait(false);
            await uow.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            throw;
        }

        await _orderDetailCache.InvalidateAsync(m.OrderId, context.CancellationToken).ConfigureAwait(false);
    }
}
