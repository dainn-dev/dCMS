using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using dCMS.Payment.Infrastructure.Webhooks;
using dCMS.Payment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Payment.Api.Routes;

/// <summary>DAI-341 — gateway-to-service webhook (HMAC); publishes saga payment events.</summary>
public static class PaymentWebhookRoutes
{
    public static void MapPaymentGatewayWebhookRoutes(this WebApplication app)
    {
        app.MapPost("/api/webhooks/payment/{provider}", HandleWebhook)
            .WithName("PaymentGatewayWebhook")
            .WithTags("payments");
    }

    private static async Task<IResult> HandleWebhook(
        string provider,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] PaymentGatewayWebhookProcessor processor,
        CancellationToken cancellationToken)
    {
        provider = (provider ?? "").Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return Results.Json(
                new { error = new { code = "INVALID_PROVIDER", message = "provider is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var secret = configuration[$"Payment:Providers:{provider}:WebhookSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Results.Json(
                new { error = new { code = "UNKNOWN_PROVIDER", message = "No webhook secret configured for provider." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        var signature = http.Request.Headers["X-Payment-Signature"].FirstOrDefault();
        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var bodyBytes = Encoding.UTF8.GetBytes(raw);

        if (!PaymentWebhookVerifier.VerifyHmacSha256(signature, secret, bodyBytes))
        {
            return Results.Json(
                new { error = new { code = "INVALID_SIGNATURE", message = "Invalid signature." } },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var signatureDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature ?? ""))).ToLowerInvariant();

        if (!TryParseEnvelope(
                raw,
                out var paymentIntentId,
                out var tenantId,
                out var clientId,
                out var eventId,
                out var succeeded,
                out var providerPaymentId,
                out var failureReason,
                out var occurredAt,
                out var parseErr))
        {
            return Results.Json(
                new { error = new { code = "INVALID_PAYLOAD", message = parseErr } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (DateTimeOffset.UtcNow - occurredAt > TimeSpan.FromMinutes(5))
        {
            return Results.Json(
                new { error = new { code = "REPLAY_REJECTED", message = "occurredAt is too old." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        eventId = string.IsNullOrWhiteSpace(eventId) ? $"sig:{signatureDigest}" : eventId.Trim();
        clientId = string.IsNullOrWhiteSpace(clientId)
            ? configuration.GetSection("Dcms:Client")["Id"]?.Trim() ?? "aeon"
            : clientId.Trim();

        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return Results.Json(
                new { error = new { code = "INVALID_TENANT", message = "tenantId must be a valid UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var repository = http.RequestServices.GetRequiredService<IPaymentTransactionRepository>();
        var recorded = await repository
            .TryRecordWebhookDeliveryAsync(provider, eventId, signatureDigest, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        if (!recorded)
        {
            return Results.Ok();
        }

        var result = await processor
            .ProcessAsync(paymentIntentId, tenantGuid, clientId, provider, succeeded, providerPaymentId, failureReason, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            PaymentWebhookProcessResult.Ok or PaymentWebhookProcessResult.OkAlreadyProcessed => Results.Ok(),
            PaymentWebhookProcessResult.UnknownIntent => Results.Accepted(),
            PaymentWebhookProcessResult.Conflict => Results.Json(
                new { error = new { code = "CONFLICT", message = "Event does not match current payment state." } },
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static bool TryParseEnvelope(
        string rawJson,
        out string paymentIntentId,
        out string tenantId,
        out string clientId,
        out string eventId,
        out bool succeeded,
        out string? providerPaymentId,
        out string failureReason,
        out DateTimeOffset occurredAt,
        out string error)
    {
        paymentIntentId = "";
        tenantId = "";
        clientId = "";
        eventId = "";
        succeeded = false;
        providerPaymentId = null;
        failureReason = "";
        occurredAt = default;
        error = "";

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            paymentIntentId =
                GetString(root, "paymentIntentId")
                ?? GetString(root, "payment_intent_id")
                ?? "";
            tenantId =
                GetString(root, "tenantId")
                ?? GetString(root, "tenant_id")
                ?? "";
            clientId =
                GetString(root, "clientId")
                ?? GetString(root, "client_id")
                ?? "";
            eventId =
                GetString(root, "eventId")
                ?? GetString(root, "event_id")
                ?? GetString(root, "id")
                ?? "";

            var statusRaw =
                GetString(root, "status")
                ?? GetString(root, "paymentStatus")
                ?? GetString(root, "payment_status")
                ?? "";

            providerPaymentId =
                GetString(root, "providerPaymentId")
                ?? GetString(root, "provider_payment_id")
                ?? GetString(root, "chargeId")
                ?? GetString(root, "charge_id");

            failureReason =
                GetString(root, "reason")
                ?? GetString(root, "failureReason")
                ?? GetString(root, "failure_reason")
                ?? "";

            var occurred = GetString(root, "occurredAt") ?? GetString(root, "occurred_at") ?? "";

            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                error = "paymentIntentId is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                error = "tenantId is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(statusRaw))
            {
                error = "status is required.";
                return false;
            }

            statusRaw = statusRaw.Trim();
            succeeded = statusRaw.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
                || statusRaw.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || statusRaw.Equals("paid", StringComparison.OrdinalIgnoreCase);

            var failed = statusRaw.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || statusRaw.Equals("canceled", StringComparison.OrdinalIgnoreCase)
                || statusRaw.Equals("cancelled", StringComparison.OrdinalIgnoreCase);

            if (!succeeded && !failed)
            {
                error = "status must be a terminal success or failure value.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(occurred) || !DateTimeOffset.TryParse(occurred, out occurredAt))
            {
                error = "occurredAt must be an ISO-8601 datetime.";
                return false;
            }

            paymentIntentId = paymentIntentId.Trim();
            tenantId = tenantId.Trim();
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
