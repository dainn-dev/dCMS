using dCMS.Core.Messaging;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Payment.Infrastructure.Messaging;

/// <summary>US-F3 / DAI-359 — consumes <see cref="RefundPaymentV1"/>; refunds captured charges; publishes <see cref="PaymentRefundedV1"/>.</summary>
public sealed class RefundPaymentConsumer(
    IPaymentGateway gateway,
    IPaymentTransactionRepository repository,
    ILogger<RefundPaymentConsumer> logger) : IConsumer<RefundPaymentV1>
{
    public async Task Consume(ConsumeContext<RefundPaymentV1> context)
    {
        var m = context.Message;
        var at = DateTimeOffset.UtcNow;

        if (!Guid.TryParse(m.OrderId, out var orderId))
        {
            logger.LogWarning("RefundPaymentV1 skipped: OrderId is not a GUID.");
            return;
        }

        if (!Guid.TryParse(m.TenantId, out var msgTenant))
        {
            logger.LogWarning("RefundPaymentV1 skipped: TenantId is not a GUID.");
            return;
        }

        var row = await repository
            .GetLatestByOrderIdAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            logger.LogWarning("RefundPaymentV1 skipped: no PaymentTransaction for order {OrderId}.", m.OrderId);
            return;
        }

        if (msgTenant != row.TenantId)
        {
            logger.LogWarning("RefundPaymentV1 skipped: tenant mismatch for order {OrderId}.", m.OrderId);
            return;
        }

        if (row.Amount != m.Amount            || !string.Equals(row.Currency, m.Currency, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("RefundPaymentV1 skipped: amount/currency mismatch for order {OrderId}.", m.OrderId);
            return;
        }

        if (row.Status == PaymentTransactionStatus.Refunded)
        {
            var rid = $"re_stub_{row.PaymentIntentId}";
            var storeStr = row.StoreId.ToString("D");
            await context
                .Publish(
                    new PaymentRefundedV1(m.OrderId, rid, m.Amount, m.TenantId, storeStr, at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (row.Status != PaymentTransactionStatus.Succeeded)
        {
            logger.LogWarning(
                "RefundPaymentV1 skipped: transaction {TxId} status {Status} is not refundable (expected succeeded).",
                row.Id,
                row.Status);
            return;
        }

        var storeId = row.StoreId.ToString("D");

        var result = await gateway
            .RefundPaymentAsync(
                new RefundPaymentGatewayRequest(
                    row.PaymentIntentId,
                    row.OrderId,
                    row.TenantId,
                    m.Amount,
                    m.Currency,
                    m.Reason),
                context.CancellationToken)
            .ConfigureAwait(false);

        switch (result)
        {
            case RefundPaymentGatewayResult.Succeeded ok:
                await repository
                    .UpdateStatusByIdAsync(row.Id, "refunded", context.CancellationToken)
                    .ConfigureAwait(false);
                await context
                    .Publish(
                        new PaymentRefundedV1(m.OrderId, ok.RefundId, m.Amount, m.TenantId, storeId, at),
                        context.CancellationToken)
                    .ConfigureAwait(false);
                return;
            case RefundPaymentGatewayResult.AlreadyRefunded ok:
                await repository
                    .UpdateStatusByIdAsync(row.Id, "refunded", context.CancellationToken)
                    .ConfigureAwait(false);
                await context
                    .Publish(
                        new PaymentRefundedV1(m.OrderId, ok.RefundId, m.Amount, m.TenantId, storeId, at),
                        context.CancellationToken)
                    .ConfigureAwait(false);
                return;
            case RefundPaymentGatewayResult.Failed err:
                logger.LogError(
                    "RefundPaymentV1 gateway failed for order {OrderId}: {Code}",
                    m.OrderId,
                    err.ErrorCode);
                return;
            default:
                throw new InvalidOperationException($"Unexpected refund result: {result.GetType().Name}");
        }
    }
}
