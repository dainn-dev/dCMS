using System.Text.Json;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Shipping;

/// <summary>DAI-335 — periodically polls carrier tracking APIs for stale shipments and records events.</summary>
public sealed class ShipmentPollingWorker(
    IConfiguration configuration,
    ShipmentPollingStore store,
    ICarrierTrackingClient trackingClient,
    ICarrierStatusMapper statusMapper,
    ILogger<ShipmentPollingWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(configuration.GetValue<int?>("Shipment:Polling:IntervalMinutes") ?? 30);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Shipment polling tick failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var staleForMinutes = configuration.GetValue<int?>("Shipment:Polling:StaleAfterMinutes") ?? 60;
        var batchSize = configuration.GetValue<int?>("Shipment:Polling:BatchSize") ?? 200;
        var defaultDelayMs = configuration.GetValue<int?>("Shipment:Polling:DefaultDelayMs") ?? 200;

        var stale = await store
            .ListStaleAsync(batchSize, TimeSpan.FromMinutes(staleForMinutes), cancellationToken)
            .ConfigureAwait(false);

        if (stale.Count == 0)
            return;

        foreach (var group in stale.GroupBy(s => s.Carrier ?? "", StringComparer.OrdinalIgnoreCase))
        {
            var carrier = group.Key?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(carrier))
                continue;

            var delayMs = configuration.GetValue<int?>($"Shipment:Carriers:{carrier}:PollDelayMs") ?? defaultDelayMs;
            var delay = TimeSpan.FromMilliseconds(Math.Clamp(delayMs, 0, 60_000));

            foreach (var s in group)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(s.Status) &&
                    (string.Equals(s.Status, "delivered", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(s.Status, "failed", StringComparison.OrdinalIgnoreCase)))
                    continue;

                var latest = await trackingClient
                    .GetLatestAsync(carrier, s.TrackingNumber, cancellationToken)
                    .ConfigureAwait(false);
                if (latest is null)
                    continue;

                var mapped = statusMapper.Map(carrier, latest.ExternalStatus);
                if (mapped == MappedStatus.Unknown)
                    continue;

                var mappedDb = mapped.ToDbValue();
                var payload = JsonSerializer.Serialize(new
                {
                    source = "polling",
                    carrier,
                    trackingNumber = s.TrackingNumber,
                    externalStatus = latest.ExternalStatus,
                    mappedStatus = mappedDb,
                    occurredAt = latest.OccurredAt,
                    location = latest.Location,
                    raw = TryParseJson(latest.RawPayloadJson),
                }, Json);

                await using var uow = new OrderUnitOfWork(configuration.GetConnectionString("Order")!);
                await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var r = await uow.TryUpsertShipmentEventFromPollingAsync(
                            s.Id,
                            mappedDb,
                            latest.Location,
                            latest.OccurredAt,
                            payload,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await uow.CommitAsync(cancellationToken).ConfigureAwait(false);

                    if (r == ShipmentPollingDbOutcome.Ok)
                        logger.LogInformation("Shipment {ShipmentId} updated to {Status}", s.Id, mappedDb);
                }
                catch
                {
                    await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static JsonElement TryParseJson(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(raw);
        }
        catch
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }
}

