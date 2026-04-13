namespace dCMS.Payment.Core;

/// <summary>External payment provider / gateway (stub in dev).</summary>
public interface IPaymentGateway
{
    Task<PaymentGatewayIntent> CreateIntentAsync(
        CreatePaymentIntentGatewayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge/capture using <paramref name="request"/>.PaymentIntentId as the provider idempotency key.
    /// Duplicate successful calls should return <see cref="ProcessPaymentGatewayResult.AlreadySucceeded"/>.
    /// </summary>
    Task<ProcessPaymentGatewayResult> ProcessPaymentAsync(
        ProcessPaymentGatewayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refund a captured charge. <paramref name="request"/>.PaymentIntentId is the idempotency key;
    /// duplicate calls return <see cref="RefundPaymentGatewayResult.AlreadyRefunded"/>.
    /// </summary>
    Task<RefundPaymentGatewayResult> RefundPaymentAsync(
        RefundPaymentGatewayRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreatePaymentIntentGatewayRequest(
    Guid OrderId,
    Guid TenantId,
    Guid StoreId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public sealed record PaymentGatewayIntent(string PaymentIntentId, string PaymentUrl);
