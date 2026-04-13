using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Order.Core.Integration;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Integration;

/// <summary>DAI-315 — Calls Payment Service <c>POST /internal/payment/create-intent</c> (envelope <c>data.paymentIntentId</c> / <c>data.paymentUrl</c>).</summary>
public sealed class HttpPaymentClient : IPaymentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpPaymentClient> _logger;

    public HttpPaymentClient(HttpClient http, ILogger<HttpPaymentClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = new CreateIntentHttpBody(
            request.OrderId,
            request.TenantId,
            request.StoreId,
            request.CustomerId,
            request.Amount,
            request.Currency);

        using var response = await _http
            .PostAsJsonAsync("internal/payment/create-intent", body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Payment create-intent HTTP {Status} for order {OrderId}: {Body}",
                (int)response.StatusCode,
                request.OrderId,
                payload);
            throw new PaymentInitException(
                $"Payment service returned HTTP {(int)response.StatusCode}: {Truncate(payload, 500)}")
            {
                ServiceErrorCode = "HTTP_ERROR",
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            {
                var code = err.TryGetProperty("code", out var c) ? c.GetString() : "unknown";
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : payload;
                throw new PaymentInitException($"Payment create-intent failed ({code}): {msg}")
                {
                    ServiceErrorCode = code,
                };
            }

            if (TryReadIntent(root, out var intentId, out var paymentUrl))
                return new PaymentIntentResult(intentId, paymentUrl);

            throw new PaymentInitException("Payment create-intent returned no paymentIntentId/paymentUrl.");
        }
        catch (PaymentInitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PaymentInitException($"Invalid payment create-intent response: {Truncate(payload, 500)}", ex);
        }
    }

    private static bool TryReadIntent(JsonElement root, out string intentId, out string paymentUrl)
    {
        intentId = "";
        paymentUrl = "";

        JsonElement data;
        if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
            data = d;
        else
            data = root;

        if (!data.TryGetProperty("paymentIntentId", out var pi) || pi.ValueKind != JsonValueKind.String)
            return false;
        if (!data.TryGetProperty("paymentUrl", out var pu) || pu.ValueKind != JsonValueKind.String)
            return false;

        intentId = pi.GetString() ?? "";
        paymentUrl = pu.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(intentId) && !string.IsNullOrWhiteSpace(paymentUrl);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private sealed record CreateIntentHttpBody(
        string OrderId,
        string TenantId,
        string StoreId,
        string CustomerId,
        decimal Amount,
        string Currency);
}
