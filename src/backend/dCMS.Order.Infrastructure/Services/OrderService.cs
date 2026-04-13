using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Infrastructure.Services;

public sealed class OrderService : IOrderService
{
    private readonly string _connectionString;
    private readonly OrderQueryStore _queryStore;
    private readonly IInventoryClient _inventoryClient;
    private readonly IPaymentClient _paymentClient;

    public OrderService(
        IConfiguration configuration,
        OrderQueryStore queryStore,
        IInventoryClient inventoryClient,
        IPaymentClient paymentClient)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _queryStore = queryStore;
        _inventoryClient = inventoryClient;
        _paymentClient = paymentClient;
    }

    public Task<Core.Domain.Order?> GetByIdAsync(string tenantId, string storeId, string orderId, CancellationToken cancellationToken = default) =>
        _queryStore.GetByIdAsync(tenantId, storeId, orderId, cancellationToken);

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

        var order = Core.Domain.Order.Create(
            command.OrderId,
            command.TenantId,
            command.StoreId,
            command.CustomerId,
            items,
            command.ShippingAddress,
            command.OccurredAt);

        var payment = await _paymentClient
            .CreatePaymentIntentAsync(
                new CreatePaymentIntentRequest(
                    command.OrderId,
                    command.TenantId,
                    command.StoreId,
                    command.CustomerId,
                    order.Total.Amount,
                    order.Total.Currency),
                cancellationToken)
            .ConfigureAwait(false);

        order.AssignPaymentIntent(payment.PaymentIntentId);

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

        return new CreateOrderResult(order, payment.PaymentUrl, IsIdempotentReplay: false);
    }
}
