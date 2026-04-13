namespace dCMS.Core.Exceptions;

/// <summary>Non-retryable business rule violation for MassTransit consumers (US-F1 / DAI-348).</summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
