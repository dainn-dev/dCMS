using dCMS.Core.Messaging;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Payment.Infrastructure.Webhooks;

/// <summary>DAI-341 — applies gateway webhook outcomes to <see cref="PaymentTransaction"/> and notifies Order saga.</summary>
public class PaymentGatewayWebhookProcessor(
    IPaymentTransactionRepository repository,
    IBus bus,
    ILogger<PaymentGatewayWebhookProcessor> logger)
{
    public virtual async Task<PaymentWebhookProcessResult> ProcessAsync(
        string paymentIntentId,
        Guid tenantId,
        string clientId,
        string provider,
        bool succeeded,
        string? providerPaymentId,
        string failureReason,
        CancellationToken cancellationToken)
    {
        paymentIntentId = paymentIntentId.Trim();
        var row = await repository
            .GetLatestByPaymentIntentIdAsync(paymentIntentId, tenantId, clientId, provider, null, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return PaymentWebhookProcessResult.UnknownIntent;

        if (row.Status == PaymentTransactionStatus.Succeeded)
        {
            if (succeeded)
                return PaymentWebhookProcessResult.OkAlreadyProcessed;
            logger.LogWarning(
                "Payment webhook conflict: intent {Intent} already succeeded but event is failure.",
                paymentIntentId);
            return PaymentWebhookProcessResult.Conflict;
        }

        if (row.Status == PaymentTransactionStatus.Failed)
        {
            if (!succeeded)
                return PaymentWebhookProcessResult.OkAlreadyProcessed;
            logger.LogWarning(
                "Payment webhook conflict: intent {Intent} already failed but event is success.",
                paymentIntentId);
            return PaymentWebhookProcessResult.Conflict;
        }

        var at = DateTimeOffset.UtcNow;
        var tenantIdStr = row.TenantId.ToString("D");
        var storeIdStr = row.StoreId.ToString("D");
        var orderIdStr = row.OrderId.ToString("D");

        if (succeeded)
        {
            await repository.UpdateStatusByIdAsync(
                    row.Id,
                    row.TenantId,
                    row.StoreId,
                    row.ClientId,
                    row.Provider,
                    "completed",
                    cancellationToken)
                .ConfigureAwait(false);
            var payId = string.IsNullOrWhiteSpace(providerPaymentId) ? row.PaymentIntentId : providerPaymentId.Trim();
            await bus
                .Publish(new PaymentCompletedV1(orderIdStr, payId, tenantIdStr, storeIdStr, at), cancellationToken)
                .ConfigureAwait(false);
            return PaymentWebhookProcessResult.Ok;
        }

        var reason = string.IsNullOrWhiteSpace(failureReason) ? "gateway_webhook_failed" : failureReason.Trim();
        await repository.UpdateStatusByIdAsync(
                row.Id,
                row.TenantId,
                row.StoreId,
                row.ClientId,
                row.Provider,
                "failed",
                cancellationToken)
            .ConfigureAwait(false);
        await bus
            .Publish(new PaymentFailedV1(orderIdStr, reason, tenantIdStr, storeIdStr, at), cancellationToken)
            .ConfigureAwait(false);
        return PaymentWebhookProcessResult.Ok;
    }
}

public enum PaymentWebhookProcessResult
{
    Ok,
    OkAlreadyProcessed,
    UnknownIntent,
    Conflict,
}
