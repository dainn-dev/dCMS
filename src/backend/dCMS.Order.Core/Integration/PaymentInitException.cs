namespace dCMS.Order.Core.Integration;

/// <summary>
/// Raised when Payment Service fails to create an intent (US-18 / DAI-315).
/// Map to HTTP 422 with code <c>PAYMENT_INIT_FAILED</c> at the API layer.
/// </summary>
public sealed class PaymentInitException : Exception
{
    public PaymentInitException(string message)
        : base(message)
    {
    }

    public PaymentInitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Optional machine-readable code from Payment Service error envelope.</summary>
    public string? ServiceErrorCode { get; init; }
}
