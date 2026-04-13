namespace dCMS.Order.Api.Contracts;

/// <summary>US-21 / DAI-326 — optional body for <c>POST /api/orders/{id}/cancel</c>.</summary>
public sealed class CancelOrderApiRequest
{
    /// <summary>Human-readable reason (defaults to <c>customer_request</c> when omitted).</summary>
    public string? Reason { get; set; }
}
