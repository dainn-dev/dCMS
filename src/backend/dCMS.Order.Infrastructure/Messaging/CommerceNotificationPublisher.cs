using System.Text.Json;
using Dapper;
using dCMS.Core.Messaging;
using dCMS.Messaging.Contracts.Messaging;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Order.Infrastructure.Messaging;

/// <summary>DAI-717 — publishes <see cref="EmailQueuedV1"/> for commerce lifecycle events (order, payment, fulfillment).</summary>
public sealed class CommerceNotificationPublisher(
    OrderQueryStore orders,
    IConfiguration configuration,
    ILogger<CommerceNotificationPublisher> log) :
    IConsumer<OrderPaymentSettledV1>,
    IConsumer<OrderCancelledV1>,
    IConsumer<PaymentFailedV1>,
    IConsumer<OrderShippedV1>,
    IConsumer<PaymentRefundedV1>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private string OrderConnectionString =>
        configuration.GetConnectionString("Order")
        ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

    public Task Consume(ConsumeContext<OrderPaymentSettledV1> context) =>
        PublishOrderEmailAsync(context, context.Message.OrderId, context.Message.TenantId, context.Message.StoreId,
            "order.confirmation", "payment_settled", context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelledV1> context) =>
        PublishOrderEmailAsync(context, context.Message.OrderId, context.Message.TenantId, context.Message.StoreId,
            "order.cancelled", "order_cancelled", context.CancellationToken);

    public Task Consume(ConsumeContext<PaymentFailedV1> context) =>
        PublishOrderEmailAsync(context, context.Message.OrderId, context.Message.TenantId, context.Message.StoreId,
            "payment.failed", "payment_failed", context.CancellationToken);

    public Task Consume(ConsumeContext<OrderShippedV1> context) =>
        PublishOrderEmailAsync(context, context.Message.OrderId, null, null,
            "order.shipped", "order_shipped", context.CancellationToken);

    public Task Consume(ConsumeContext<PaymentRefundedV1> context) =>
        PublishOrderEmailAsync(context, context.Message.OrderId, context.Message.TenantId, context.Message.StoreId,
            "order.cancelled", "payment_refunded", context.CancellationToken);

    private async Task PublishOrderEmailAsync(
        IPublishEndpoint endpoint,
        string orderId,
        string? tenantId,
        string? storeId,
        string templateKey,
        string eventKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId))
        {
            var scope = await TryLoadOrderScopeAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (scope is null)
            {
                log.LogWarning("Commerce notification skipped: order {OrderId} not found for template {Template}", orderId, templateKey);
                return;
            }

            tenantId = scope.Value.TenantId;
            storeId = scope.Value.StoreId;
        }

        var order = await orders.GetByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            log.LogWarning("Commerce notification skipped: order {OrderId} not found", orderId);
            return;
        }

        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            log.LogInformation("Commerce notification skipped: order {OrderId} has no customer email", orderId);
            return;
        }

        var model = new
        {
            orderId = order.Id,
            customerName = order.CustomerName ?? order.CustomerId,
            orderDate = DateTimeOffset.UtcNow.ToString("dd MMM yyyy"),
            total = $"{order.Total.Amount} {order.Total.Currency}",
            storeName = storeId,
            trackingNumber = "",
            carrier = "",
        };

        var idempotencyKey = $"order:{order.Id}:{eventKey}:{templateKey}";
        await endpoint.Publish(
                new EmailQueuedV1(
                    idempotencyKey,
                    tenantId,
                    storeId,
                    templateKey,
                    Locale: "en",
                    order.CustomerEmail,
                    Channel: "email",
                    JsonSerializer.Serialize(model, JsonOptions),
                    DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(string TenantId, string StoreId)?> TryLoadOrderScopeAsync(string orderId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(orderId, out var id))
            return null;

        await using var conn = new NpgsqlConnection(OrderConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await conn.QuerySingleOrDefaultAsync<OrderScopeRow>(
            new CommandDefinition(
                """SELECT "TenantId", "StoreId" FROM "Orders" WHERE "Id" = @Id LIMIT 1""",
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null || string.IsNullOrWhiteSpace(row.TenantId)
            ? null
            : (row.TenantId, row.StoreId);
    }

    private sealed class OrderScopeRow
    {
        public string TenantId { get; init; } = "";
        public string StoreId { get; init; } = "";
    }
}
