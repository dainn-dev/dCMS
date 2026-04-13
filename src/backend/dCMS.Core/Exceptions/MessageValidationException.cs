namespace dCMS.Core.Exceptions;

/// <summary>Non-retryable validation failure for MassTransit consumers (US-F1 / DAI-348).</summary>
public sealed class MessageValidationException : Exception
{
    public MessageValidationException(string message)
        : base(message)
    {
    }

    public MessageValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
