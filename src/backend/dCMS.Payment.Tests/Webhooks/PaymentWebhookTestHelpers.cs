using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dCMS.Payment.Tests.Webhooks;

public static class PaymentWebhookTestHelpers
{
    public static string Sign(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static HttpRequestMessage BuildRequest(string provider, string body, string signature)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/payment/{provider}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("X-Payment-Signature", signature);
        return req;
    }

    public static string ValidBody(
        string intentId,
        string tenantId,
        bool success = true,
        DateTimeOffset? occurredAt = null,
        string? eventId = null)
    {
        var ts = (occurredAt ?? DateTimeOffset.UtcNow).ToString("O");
        var status = success ? "succeeded" : "failed";
        return JsonSerializer.Serialize(new
        {
            paymentIntentId = intentId,
            tenantId,
            status,
            occurredAt = ts,
            eventId,
        });
    }
}
