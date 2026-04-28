using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Order.Core.Integration;
using dCMS.Promotions.Contracts.Evaluate;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Integration;

/// <summary>
/// DAI-693 — Calls Promotions <c>/api/v1/tenants/{tenantId}/promotions/{evaluate|redemptions/...}</c>.
/// Polly retry (2x, 200ms) is wired in <c>OrderServiceCollectionExtensions.AddPromotionsHttpClient</c>.
/// </summary>
public sealed class HttpPromotionsClient : IPromotionsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpPromotionsClient> _logger;

    public HttpPromotionsClient(HttpClient http, ILogger<HttpPromotionsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<EvaluateResponse?> EvaluateAsync(
        EvaluateRequest request, CancellationToken cancellationToken = default)
    {
        var path = $"api/v1/tenants/{Uri.EscapeDataString(request.TenantId)}/promotions/evaluate";
        using var response = await _http
            .PostAsJsonAsync(path, request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Promotions evaluate HTTP {Status} for tenant {TenantId}: {Body}",
                (int)response.StatusCode, request.TenantId, payload);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            throw new InvalidOperationException("Promotions evaluate returned no data envelope.");

        return JsonSerializer.Deserialize<EvaluateResponse>(data.GetRawText(), JsonOptions);
    }

    public Task ConfirmRedemptionAsync(
        string tenantId, ConfirmRedemptionRequest request, CancellationToken cancellationToken = default) =>
        PostAsync($"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/promotions/redemptions/confirm",
            request, cancellationToken);

    public Task ReleaseRedemptionAsync(
        string tenantId, ReleaseRedemptionRequest request, CancellationToken cancellationToken = default) =>
        PostAsync($"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/promotions/redemptions/release",
            request, cancellationToken);

    private async Task PostAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        using var response = await _http
            .PostAsJsonAsync(path, body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Promotions HTTP {Status} {Path}: {Body}",
                (int)response.StatusCode, path, payload);
            response.EnsureSuccessStatusCode();
        }
    }
}
