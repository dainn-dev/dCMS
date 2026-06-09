using dCMS.Core.Messaging;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Payment.Infrastructure.Messaging;

/// <summary>DAI-340 — consumes <see cref="ProcessPaymentV1"/> from Order saga; charges via gateway (idempotent on payment intent id).</summary>
public sealed class ProcessPaymentConsumer(
    IPaymentGateway gateway,
    IPaymentTransactionRepository repository,
    IConfiguration configuration,
    ILogger<ProcessPaymentConsumer> logger) : IConsumer<ProcessPaymentV1>
{
    public async Task Consume(ConsumeContext<ProcessPaymentV1> context)
    {
        var m = context.Message;
        var at = DateTimeOffset.UtcNow;

        if (!Guid.TryParse(m.OrderId, out var orderId))
        {
            logger.LogWarning("ProcessPaymentV1 skipped: OrderId is not a GUID.");
            return;
        }

        if (!Guid.TryParse(m.TenantId, out var msgTenant))
        {
            logger.LogWarning("ProcessPaymentV1 skipped: TenantId is not a GUID.");
            return;
        }

        var clientId = ResolveClientId(configuration);
        const string provider = "stub";

        var row = await repository
            .GetLatestByOrderIdAsync(orderId, msgTenant, clientId, provider, context.CancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            await context
                .Publish(
                    new PaymentFailedV1(m.OrderId, "payment_transaction_not_found", m.TenantId, string.Empty, at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (msgTenant != row.TenantId)
        {
            await context
                .Publish(
                    new PaymentFailedV1(
                        m.OrderId,
                        "tenant_mismatch",
                        m.TenantId,
                        row.StoreId.ToString("D"),
                        at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (row.Amount != m.Amount
            || !string.Equals(row.Currency, m.Currency, StringComparison.OrdinalIgnoreCase))
        {
            await context
                .Publish(
                    new PaymentFailedV1(
                        m.OrderId,
                        "payment_amount_mismatch",
                        m.TenantId,
                        row.StoreId.ToString("D"),
                        at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!string.Equals(row.PaymentMethod, m.PaymentMethod, StringComparison.OrdinalIgnoreCase))
        {
            await context
                .Publish(
                    new PaymentFailedV1(
                        m.OrderId,
                        "payment_method_mismatch",
                        m.TenantId,
                        row.StoreId.ToString("D"),
                        at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var storeId = row.StoreId.ToString("D");

        if (row.Status == PaymentTransactionStatus.Succeeded)
        {
            await context
                .Publish(
                    new PaymentCompletedV1(m.OrderId, row.PaymentIntentId, m.TenantId, storeId, at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (row.Status == PaymentTransactionStatus.Failed)
        {
            await context
                .Publish(
                    new PaymentFailedV1(m.OrderId, "previously_failed", m.TenantId, storeId, at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var result = await gateway
            .ProcessPaymentAsync(
                new ProcessPaymentGatewayRequest(
                    row.PaymentIntentId,
                    row.OrderId,
                    row.TenantId,
                    m.Amount,
                    m.Currency,
                    m.PaymentMethod),
                context.CancellationToken)
            .ConfigureAwait(false);

        switch (result)
        {
            case ProcessPaymentGatewayResult.Succeeded ok:
                await repository
                    .UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, row.ClientId, row.Provider, "completed", context.CancellationToken)
                    .ConfigureAwait(false);
                await context
                    .Publish(
                        new PaymentCompletedV1(m.OrderId, ok.ProviderPaymentId, m.TenantId, storeId, at),
                        context.CancellationToken)
                    .ConfigureAwait(false);
                return;
            case ProcessPaymentGatewayResult.AlreadySucceeded ok:
                await repository
                    .UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, row.ClientId, row.Provider, "completed", context.CancellationToken)
                    .ConfigureAwait(false);
                await context
                    .Publish(
                        new PaymentCompletedV1(m.OrderId, ok.ProviderPaymentId, m.TenantId, storeId, at),
                        context.CancellationToken)
                    .ConfigureAwait(false);
                return;
            case ProcessPaymentGatewayResult.Failed err:
                await repository
                    .UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, row.ClientId, row.Provider, "failed", context.CancellationToken)
                    .ConfigureAwait(false);
                await context
                    .Publish(
                        new PaymentFailedV1(m.OrderId, err.ErrorCode, m.TenantId, storeId, at),
                        context.CancellationToken)
                    .ConfigureAwait(false);
                return;
            default:
                throw new InvalidOperationException($"Unexpected gateway result: {result.GetType().Name}");
        }
    }

    private static string ResolveClientId(IConfiguration configuration) =>
        configuration.GetSection("Dcms:Client")["Id"]?.Trim() ?? "aeon";
}
