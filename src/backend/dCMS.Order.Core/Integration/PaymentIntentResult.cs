namespace dCMS.Order.Core.Integration;

/// <summary>Successful response from Payment Service create-intent (DAI-315).</summary>
public sealed record PaymentIntentResult(string PaymentIntentId, string PaymentUrl);
