namespace dCMS.Order.Core.Integration;

/// <summary>Sync payment intent creation (DAI-315 / US-18).</summary>
public interface IPaymentClient
{
    /// <summary>
    /// Calls Payment Service <c>POST /internal/payment/create-intent</c>.
    /// Throws <see cref="PaymentInitException"/> when the call fails or returns a business error.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);
}
