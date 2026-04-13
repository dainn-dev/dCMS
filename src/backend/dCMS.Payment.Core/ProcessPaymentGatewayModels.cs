namespace dCMS.Payment.Core;

public sealed record ProcessPaymentGatewayRequest(
    string PaymentIntentId,
    Guid OrderId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public abstract record ProcessPaymentGatewayResult
{
    public sealed record Succeeded(string ProviderPaymentId) : ProcessPaymentGatewayResult;

    public sealed record AlreadySucceeded(string ProviderPaymentId) : ProcessPaymentGatewayResult;

    public sealed record Failed(string ErrorCode) : ProcessPaymentGatewayResult;
}

public sealed record RefundPaymentGatewayRequest(
    string PaymentIntentId,
    Guid OrderId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string Reason);

public abstract record RefundPaymentGatewayResult
{
    public sealed record Succeeded(string RefundId) : RefundPaymentGatewayResult;

    public sealed record AlreadyRefunded(string RefundId) : RefundPaymentGatewayResult;

    public sealed record Failed(string ErrorCode) : RefundPaymentGatewayResult;
}
