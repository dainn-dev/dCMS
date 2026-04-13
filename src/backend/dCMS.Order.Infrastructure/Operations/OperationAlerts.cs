using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Operations;

public sealed class OperationAlerts(
    IConfiguration configuration,
    ILogger<OperationAlerts> logger,
    IHttpClientFactory httpClientFactory) : IOperationAlerts
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task NotifyLatePaymentCompletedOnCancelledAsync(
        string orderId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Late PaymentCompleted on cancelled order {OrderId} (tenant {TenantId})",
            orderId,
            tenantId);

        var url = configuration["Operations:SlackWebhookUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            var client = httpClientFactory.CreateClient(nameof(OperationAlerts));
            var body = JsonSerializer.SerializeToUtf8Bytes(
                new { text = $"Late PaymentCompleted on cancelled order `{orderId}` (tenant `{tenantId}`)." },
                JsonOptions);
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await client
                .PostAsync(new Uri(url, UriKind.Absolute), content, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Slack webhook returned {Status} for late payment alert.", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Slack webhook failed for late payment alert.");
        }
    }
}
