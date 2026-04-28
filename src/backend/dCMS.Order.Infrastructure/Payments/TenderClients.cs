using System.Net.Http.Json;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-724: Outcome of a downstream tender call (Voucher.Api / Loyalty.Api).
/// <see cref="ExternalRef"/> is the holdId returned by Reserve, or null on capture/release/refund.
/// </summary>
public readonly record struct TenderCallResult(bool Success, string? ExternalRef, string? ErrorCode, string? ErrorMessage)
{
    public static TenderCallResult Ok(string? externalRef = null) => new(true, externalRef, null, null);
    public static TenderCallResult Fail(string code, string message) => new(false, null, code, message);
}

public interface IVoucherTenderClient
{
    Task<TenderCallResult> ReserveAsync(string tenantId, string code, Guid orderId, decimal amount, CancellationToken ct);
    Task<TenderCallResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct);
    Task<TenderCallResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct);
    Task<TenderCallResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct);
}

public interface ILoyaltyTenderClient
{
    Task<TenderCallResult> ReserveAsync(string tenantId, string customerId, Guid orderId, decimal amount, CancellationToken ct);
    Task<TenderCallResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct);
    Task<TenderCallResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct);
    Task<TenderCallResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct);
}

/// <summary>
/// DAI-689: gateway tender client. Authorize creates an external charge intent and
/// returns a chargeRef; capture commits; void releases an uncaptured authorization;
/// refund returns funds on a captured charge. Idempotent on chargeRef.
/// </summary>
public interface IGatewayTenderClient
{
    Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct);
    Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct);
    Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct);
    Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct);
}

public abstract class TenderHttpClientBase
{
    protected readonly HttpClient Http;

    protected TenderHttpClientBase(HttpClient http)
    {
        Http = http;
    }

    protected static async Task<TenderCallResult> SendAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadFromJsonAsync<EnvelopeWithHold>(cancellationToken: ct).ConfigureAwait(false);
            return TenderCallResult.Ok(body?.Data?.HoldId);
        }
        var err = await TryReadErrorAsync(resp, ct);
        return TenderCallResult.Fail(err.Code, err.Message);
    }

    private static async Task<(string Code, string Message)> TryReadErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<EnvelopeWithError>(cancellationToken: ct).ConfigureAwait(false);
            if (body?.Error is { } e && !string.IsNullOrEmpty(e.Code))
                return (e.Code!, e.Message ?? resp.ReasonPhrase ?? "downstream error");
        }
        catch { /* fall through to status-code-only error */ }
        return ($"http_{(int)resp.StatusCode}", resp.ReasonPhrase ?? "downstream error");
    }

    private sealed record EnvelopeWithHold(HoldData? Data);
    private sealed record HoldData(string? HoldId);
    private sealed record EnvelopeWithError(ErrorBody? Error);
    private sealed record ErrorBody(string? Code, string? Message);
}

public sealed class HttpVoucherTenderClient : TenderHttpClientBase, IVoucherTenderClient
{
    public HttpVoucherTenderClient(HttpClient http) : base(http) { }

    public async Task<TenderCallResult> ReserveAsync(string tenantId, string code, Guid orderId, decimal amount, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/vouchers/{Uri.EscapeDataString(code)}/reserve",
            new { orderId, amount }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/voucher-holds/{holdId}/capture",
            new { }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/voucher-holds/{holdId}/release",
            new { reason }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/voucher-holds/{holdId}/refund",
            new { }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }
}

public sealed class HttpLoyaltyTenderClient : TenderHttpClientBase, ILoyaltyTenderClient
{
    public HttpLoyaltyTenderClient(HttpClient http) : base(http) { }

    public async Task<TenderCallResult> ReserveAsync(string tenantId, string customerId, Guid orderId, decimal amount, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/loyalty/customers/{Uri.EscapeDataString(customerId)}/reserve",
            new { orderId, amount }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/loyalty-holds/{holdId}/capture",
            new { }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/loyalty-holds/{holdId}/release",
            new { reason }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/loyalty-holds/{holdId}/refund",
            new { }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }
}
