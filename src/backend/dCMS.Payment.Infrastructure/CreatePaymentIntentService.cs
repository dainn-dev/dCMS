using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Persistence;

namespace dCMS.Payment.Infrastructure;

public sealed class CreatePaymentIntentService(
    IPaymentGateway gateway,
    IPaymentTransactionRepository repository)
{
    public async Task<CreatePaymentIntentOutcome> ExecuteAsync(
        string orderId,
        string tenantId,
        string storeId,
        string customerId,
        decimal amount,
        string currency,
        string paymentMethod,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            return CreatePaymentIntentOutcome.Error("INVALID_ORDER", "orderId must be a valid UUID.");
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            return CreatePaymentIntentOutcome.Error("INVALID_TENANT", "tenantId must be a valid UUID.");
        if (!Guid.TryParse(storeId, out var storeGuid))
            return CreatePaymentIntentOutcome.Error("INVALID_STORE", "storeId must be a valid UUID.");
        if (string.IsNullOrWhiteSpace(customerId))
            return CreatePaymentIntentOutcome.Error("INVALID_CUSTOMER", "customerId is required.");
        if (amount <= 0)
            return CreatePaymentIntentOutcome.Error("INVALID_AMOUNT", "amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency))
            return CreatePaymentIntentOutcome.Error("INVALID_CURRENCY", "currency is required.");
        if (string.IsNullOrWhiteSpace(paymentMethod))
            return CreatePaymentIntentOutcome.Error("INVALID_PAYMENT_METHOD", "paymentMethod is required.");

        var gatewayRequest = new CreatePaymentIntentGatewayRequest(
            orderGuid,
            tenantGuid,
            storeGuid,
            customerId.Trim(),
            amount,
            currency.Trim(),
            paymentMethod.Trim());

        var intent = await gateway
            .CreateIntentAsync(gatewayRequest, cancellationToken)
            .ConfigureAwait(false);

        var id = Guid.NewGuid();
        await repository.InsertInitiatedAsync(
                new PaymentTransactionInsert(
                    id,
                    orderGuid,
                    tenantGuid,
                    storeGuid,
                    customerId.Trim(),
                    paymentMethod.Trim(),
                    intent.PaymentIntentId,
                    amount,
                    currency.Trim(),
                    Provider: "stub"),
                cancellationToken)
            .ConfigureAwait(false);

        return CreatePaymentIntentOutcome.Ok(intent.PaymentIntentId, intent.PaymentUrl);
    }
}

public abstract record CreatePaymentIntentOutcome
{
    public static CreatePaymentIntentOutcome Ok(string paymentIntentId, string paymentUrl) =>
        new Success(paymentIntentId, paymentUrl);

    public static CreatePaymentIntentOutcome Error(string code, string message) =>
        new ValidationError(code, message);

    public sealed record Success(string PaymentIntentId, string PaymentUrl) : CreatePaymentIntentOutcome;

    public sealed record ValidationError(string Code, string Message) : CreatePaymentIntentOutcome;
}
