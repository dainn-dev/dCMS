namespace dCMS.Order.Infrastructure.Operations;

/// <summary>US-F3 / DAI-356 — operational visibility for payment anomalies (Slack optional).</summary>
public interface IOperationAlerts
{
    Task NotifyLatePaymentCompletedOnCancelledAsync(
        string orderId,
        string tenantId,
        CancellationToken cancellationToken = default);
}
