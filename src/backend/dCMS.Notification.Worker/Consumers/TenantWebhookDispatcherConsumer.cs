using Dapper;
using dCMS.Core.Messaging;
using dCMS.Infrastructure.Platform;
using MassTransit;
using Npgsql;

namespace dCMS.Notification.Worker.Consumers;

/// <summary>DAI-52-P1-02: deliver commerce events to tenant webhook subscriptions.</summary>
public sealed class TenantWebhookDispatcherConsumer(
    TenantWebhookDispatcher dispatcher,
    IConfiguration configuration) :
    IConsumer<OrderPaymentSettledV1>,
    IConsumer<OrderCancelledV1>,
    IConsumer<PaymentFailedV1>,
    IConsumer<OrderShippedV1>
{
    public Task Consume(ConsumeContext<OrderPaymentSettledV1> context) =>
        DispatchAsync(context.Message.TenantId, "order.payment_settled", new
        {
            orderId = context.Message.OrderId,
            tenantId = context.Message.TenantId,
            storeId = context.Message.StoreId,
            occurredAt = context.Message.OccurredAt,
        }, $"order:{context.Message.OrderId}:payment_settled", context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelledV1> context) =>
        DispatchAsync(context.Message.TenantId, "order.cancelled", new
        {
            orderId = context.Message.OrderId,
            tenantId = context.Message.TenantId,
            storeId = context.Message.StoreId,
            occurredAt = context.Message.OccurredAt,
        }, $"order:{context.Message.OrderId}:cancelled", context.CancellationToken);

    public Task Consume(ConsumeContext<PaymentFailedV1> context) =>
        DispatchAsync(context.Message.TenantId, "payment.failed", new
        {
            orderId = context.Message.OrderId,
            tenantId = context.Message.TenantId,
            storeId = context.Message.StoreId,
            occurredAt = context.Message.OccurredAt,
        }, $"order:{context.Message.OrderId}:payment_failed", context.CancellationToken);

    public async Task Consume(ConsumeContext<OrderShippedV1> context)
    {
        var tenantId = await TryResolveTenantIdAsync(context.Message.OrderId, context.CancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tenantId))
            return;

        await DispatchAsync(tenantId, "order.shipped", new
        {
            orderId = context.Message.OrderId,
            occurredAt = context.Message.OccurredAt,
        }, $"order:{context.Message.OrderId}:shipped", context.CancellationToken).ConfigureAwait(false);
    }

    private Task DispatchAsync(
        string tenantId,
        string eventType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(tenantId, eventType, payload, idempotencyKey, cancellationToken);

    private async Task<string?> TryResolveTenantIdAsync(string orderId, CancellationToken cancellationToken)
    {
        var cs = configuration.GetConnectionString("Order");
        if (string.IsNullOrWhiteSpace(cs) || !Guid.TryParse(orderId, out var id))
            return null;

        await using var conn = new NpgsqlConnection(cs);
        return await conn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT "TenantId" FROM "Orders" WHERE "Id" = @Id LIMIT 1
            """,
            new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
