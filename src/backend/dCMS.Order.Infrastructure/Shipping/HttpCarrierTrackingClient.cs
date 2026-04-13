using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Shipping;

/// <summary>
/// Minimal JSON-over-HTTP tracking client for polling worker (DAI-335).
/// Expects carrier-specific <c>Shipment:Carriers:{carrier}:TrackingBaseUrl</c>.
/// GET <c>{baseUrl}/track/{trackingNumber}</c> returns JSON containing at least <c>status</c>.
/// </summary>
public sealed class HttpCarrierTrackingClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HttpCarrierTrackingClient> logger) : ICarrierTrackingClient
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<CarrierTrackingResult?> GetLatestAsync(
        string carrier,
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        carrier = (carrier ?? "").Trim();
        trackingNumber = (trackingNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(carrier) || string.IsNullOrWhiteSpace(trackingNumber))
            return null;

        var baseUrl = configuration[$"Shipment:Carriers:{carrier}:TrackingBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TrackingBaseUrl missing for carrier {Carrier}", carrier);
            return null;
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
        {
            logger.LogWarning("Invalid TrackingBaseUrl for carrier {Carrier}", carrier);
            return null;
        }

        var client = httpClientFactory.CreateClient(nameof(HttpCarrierTrackingClient));
        client.BaseAddress = baseUri;

        var path = $"track/{Uri.EscapeDataString(trackingNumber)}";
        using var resp = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Carrier tracking call failed: {Carrier} {Tracking} HTTP {Status}",
                carrier, trackingNumber, (int)resp.StatusCode);
            return null;
        }

        var raw = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!TryParse(raw, out var status, out var occurredAt, out var location))
        {
            logger.LogWarning("Carrier tracking response invalid JSON: {Carrier} {Tracking}", carrier, trackingNumber);
            return null;
        }

        return new CarrierTrackingResult(status, occurredAt, location, raw);
    }

    private static bool TryParse(string rawJson, out string status, out DateTimeOffset occurredAt, out string? location)
    {
        status = "";
        occurredAt = default;
        location = null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            status = GetString(root, "status") ?? GetString(root, "externalStatus") ?? "";
            location = GetString(root, "location");
            var occurred = GetString(root, "occurredAt") ?? GetString(root, "updatedAt");

            if (string.IsNullOrWhiteSpace(status))
                return false;

            if (!string.IsNullOrWhiteSpace(occurred) && DateTimeOffset.TryParse(occurred, out var parsed))
                occurredAt = parsed;
            else
                occurredAt = DateTimeOffset.UtcNow;

            status = status.Trim();
            location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}

