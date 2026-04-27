using dCMS.Core.Messaging;
using dCMS.Infrastructure.Outbox;
using MassTransit;

namespace dCMS.Inventory.Api.Messaging;

/// <summary>
/// P1 #3: relays Inventory outbox messages (StockUpdatedV1) to RabbitMQ.
/// Owned by the service that owns the dcms_inventory database.
/// </summary>
public sealed class InventoryOutboxRelayBackgroundService(SqlOutboxRelay relay, IBus bus, ILogger<InventoryOutboxRelayBackgroundService> log)
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
