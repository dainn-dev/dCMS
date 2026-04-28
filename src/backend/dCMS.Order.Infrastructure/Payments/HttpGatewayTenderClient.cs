using System.Net.Http.Json;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-689: production gateway tender client. Talks to <c>dCMS.Payment.Api</c>'s
/// <c>/internal/payment/...</c> surface. Authorize delegates to
/// <c>POST /internal/payment/create-intent</c> and treats paymentIntentId as chargeRef.
/// </summary>
public sealed class HttpGatewayTenderClient : TenderHttpClientBase, IGatewayTenderClient
{
    public HttpGatewayTenderClient(HttpClient http) : base(http) { }

    public async Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            "internal/payment/create-intent",
            new { orderId, tenantId, customerId, amount, currency, paymentMethod = "card" }, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return await SendAsync(resp, ct);

        var body = await resp.Content.ReadFromJsonAsync<EnvelopeWithIntent>(cancellationToken: ct).ConfigureAwait(false);
        var chargeRef = body?.Data?.PaymentIntentId;
        return string.IsNullOrEmpty(chargeRef)
            ? TenderCallResult.Fail("invalid_response", "create-intent returned no paymentIntentId")
            : TenderCallResult.Ok(chargeRef);
    }

    public async Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"internal/payment/{Uri.EscapeDataString(chargeRef)}/capture",
            new { tenantId }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"internal/payment/{Uri.EscapeDataString(chargeRef)}/void",
            new { tenantId, reason }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"internal/payment/{Uri.EscapeDataString(chargeRef)}/refund",
            new { tenantId }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    private sealed record EnvelopeWithIntent(IntentData? Data);
    private sealed record IntentData(string? PaymentIntentId);
}
