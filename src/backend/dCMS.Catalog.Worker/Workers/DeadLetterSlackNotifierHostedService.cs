using System.Net.Http.Json;
using Npgsql;

namespace dCMS.Catalog.Worker.Workers;

/// <summary>When <c>DeadLetterEvents</c> depth &gt; 0, POST a short message to Slack (US-4 AC).</summary>
public sealed class DeadLetterSlackNotifierHostedService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<DeadLetterSlackNotifierHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var webhook = configuration["Slack:DeadLetterWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhook))
            return;

        var catalogCs = configuration.GetConnectionString("Catalog");
        if (string.IsNullOrWhiteSpace(catalogCs))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(catalogCs);
                await conn.OpenAsync(stoppingToken).ConfigureAwait(false);
                await using var cmd = new NpgsqlCommand(
                    """SELECT COUNT(*)::bigint FROM "DeadLetterEvents" WHERE "ReprocessedAt" IS NULL""", conn);
                var count = (long)(await cmd.ExecuteScalarAsync(stoppingToken).ConfigureAwait(false) ?? 0L);

                if (count > 0)
                {
                    var client = httpClientFactory.CreateClient();
                    var body = new { text = $"dCMS Catalog DeadLetterEvents depth: {count} (inspect + reprocess)." };
                    await client.PostAsJsonAsync(webhook, body, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Slack dead-letter notifier failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false);
        }
    }
}
