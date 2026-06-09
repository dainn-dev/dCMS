using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using dCMS.Provisioning.Domain;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Platform;

public sealed class TenantWebhookDispatcher(
    ITenantWebhookSubscriptionRepository subscriptions,
    ITenantWebhookDeliveryRepository deliveries,
    ITenantUsageRepository usage,
    IHttpClientFactory httpClientFactory,
    ILogger<TenantWebhookDispatcher> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int MaxAttempts = 5;

    public async Task DispatchAsync(
        string tenantId,
        string eventType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var subs = await subscriptions.ListActiveByTenantAndEventAsync(tenantId, eventType, cancellationToken)
            .ConfigureAwait(false);
        if (subs.Count == 0)
            return;

        var payloadJson = JsonSerializer.Serialize(payload, Json);
        foreach (var sub in subs)
        {
            var deliveryId = await deliveries.EnqueueAsync(
                sub.Id, tenantId, eventType, payloadJson, idempotencyKey, cancellationToken).ConfigureAwait(false);
            await TryDeliverAsync(sub, deliveryId, eventType, payloadJson, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ReplayAsync(long deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await deliveries.GetByIdAsync(deliveryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Delivery {deliveryId} not found.");
        var sub = await subscriptions.GetByIdAsync(delivery.SubscriptionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Subscription {delivery.SubscriptionId} not found.");

        await TryDeliverAsync(sub, deliveryId, delivery.EventType, delivery.PayloadJson, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task TryDeliverAsync(
        TenantWebhookSubscriptionRecord sub,
        long deliveryId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("tenant-webhooks");
        var attempt = 0;
        var status = WebhookDeliveryStatus.Failed;
        int? httpStatus = null;
        string? error = null;

        while (attempt < MaxAttempts)
        {
            attempt++;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, sub.Url);
                var body = Encoding.UTF8.GetBytes(payloadJson);
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.Add("X-Dcms-Event", eventType);
                request.Headers.Add("X-Dcms-Delivery-Id", deliveryId.ToString());
                request.Headers.Add("X-Dcms-Signature", ComputeHmac(sub.Secret, body));

                var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                httpStatus = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    status = WebhookDeliveryStatus.Delivered;
                    await usage.IncrementAsync(sub.TenantId, c => c.WebhookDeliveriesDelta = 1, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                error = $"HTTP {httpStatus}";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                log.LogWarning(ex, "Webhook delivery attempt {Attempt} failed for subscription {SubscriptionId}", attempt, sub.Id);
            }

            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
        }

        if (status != WebhookDeliveryStatus.Delivered && attempt >= MaxAttempts)
            status = WebhookDeliveryStatus.DeadLetter;

        await deliveries.UpdateDeliveryResultAsync(deliveryId, status, attempt, httpStatus, error, cancellationToken)
            .ConfigureAwait(false);

        if (status == WebhookDeliveryStatus.DeadLetter)
        {
            await subscriptions.UpdateStatusAsync(sub.Id, WebhookSubscriptionStatus.Failed, sub.FailureCount + 1, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string ComputeHmac(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }
}
