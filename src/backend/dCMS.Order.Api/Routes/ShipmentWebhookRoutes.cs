using System.Text;
using System.Text.Json;
using dCMS.Order.Infrastructure.Shipping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Api.Routes;

public static class ShipmentWebhookRoutes
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapShipmentWebhookRoutes(this WebApplication app)
    {
        // DAI-333: no JWT auth (carrier-to-service).
        app.MapPost("/api/webhooks/shipment/{carrier}", HandleWebhook)
            .WithName("CarrierShipmentWebhook")
            .WithTags("shipments");
    }

    private static async Task<IResult> HandleWebhook(
        string carrier,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] ShipmentWebhookProcessor processor,
        CancellationToken cancellationToken)
    {
        carrier = (carrier ?? "").Trim();
        if (string.IsNullOrWhiteSpace(carrier))
        {
            return Results.Json(
                new { error = new { code = "INVALID_CARRIER", message = "carrier is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var secret = configuration[$"Shipment:Carriers:{carrier}:WebhookSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Results.Json(
                new { error = new { code = "UNKNOWN_CARRIER", message = "No webhook secret configured for carrier." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        var signature = http.Request.Headers["X-Carrier-Signature"].FirstOrDefault();
        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var bodyBytes = Encoding.UTF8.GetBytes(raw);

        if (!CarrierWebhookVerifier.VerifyHmacSha256(signature, secret, bodyBytes))
        {
            return Results.Json(
                new { error = new { code = "INVALID_SIGNATURE", message = "Invalid signature." } },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!TryParseEnvelope(raw, out var trackingNumber, out var externalStatus, out var occurredAt, out var parseErr))
        {
            return Results.Json(
                new { error = new { code = "INVALID_PAYLOAD", message = parseErr } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Replay protection: reject if event is older than 5 minutes.
        if (DateTimeOffset.UtcNow - occurredAt > TimeSpan.FromMinutes(5))
        {
            return Results.Json(
                new { error = new { code = "REPLAY_REJECTED", message = "occurredAt is too old." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await processor
            .ProcessAsync(carrier, trackingNumber, externalStatus, occurredAt, raw, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            ShipmentWebhookResult.Ok => Results.Ok(),
            ShipmentWebhookResult.InvalidStatus => Results.Json(
                new { error = new { code = "INVALID_STATUS", message = "Unknown status for carrier." } },
                statusCode: StatusCodes.Status400BadRequest),
            ShipmentWebhookResult.UnknownTracking => Results.Accepted(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static bool TryParseEnvelope(
        string rawJson,
        out string trackingNumber,
        out string externalStatus,
        out DateTimeOffset occurredAt,
        out string error)
    {
        trackingNumber = "";
        externalStatus = "";
        occurredAt = default;
        error = "";

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            trackingNumber = GetString(root, "trackingNumber") ?? GetString(root, "tracking_number") ?? "";
            externalStatus = GetString(root, "status") ?? GetString(root, "externalStatus") ?? "";
            var occurred = GetString(root, "occurredAt") ?? GetString(root, "occurred_at") ?? "";

            if (string.IsNullOrWhiteSpace(trackingNumber))
            {
                error = "trackingNumber is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(externalStatus))
            {
                error = "status is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(occurred) || !DateTimeOffset.TryParse(occurred, out occurredAt))
            {
                error = "occurredAt must be an ISO-8601 datetime.";
                return false;
            }

            trackingNumber = trackingNumber.Trim();
            externalStatus = externalStatus.Trim();
            return true;
        }
        catch (JsonException)
        {
            error = "Body must be JSON.";
            return false;
        }
    }

    private static string? GetString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}

