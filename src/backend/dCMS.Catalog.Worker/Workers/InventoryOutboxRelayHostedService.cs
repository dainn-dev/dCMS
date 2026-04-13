using dCMS.Core.Messaging;
using dCMS.Infrastructure.Outbox;
using MassTransit;

namespace dCMS.Catalog.Worker.Workers;

public sealed class InventoryOutboxRelayHostedService(SqlOutboxRelay relay, IBus bus, ILogger<InventoryOutboxRelayHostedService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await relay.ProcessPendingAsync((_, m) => PublishAsync(m, stoppingToken), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Inventory outbox relay iteration failed");
            }

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(object message, CancellationToken ct) =>
        message is StockUpdatedV1 m ? bus.Publish(m, ct) : Task.CompletedTask;
}
