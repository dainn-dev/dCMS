using dCMS.Core.Messaging;
using dCMS.Infrastructure.Outbox;
using MassTransit;

namespace dCMS.Catalog.Worker.Workers;

public sealed class CatalogOutboxRelayHostedService(SqlOutboxRelay relay, IBus bus, ILogger<CatalogOutboxRelayHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await relay.ProcessPendingAsync(m => PublishAsync(m, stoppingToken), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Catalog outbox relay iteration failed");
            }

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(object message, CancellationToken ct) =>
        message switch
        {
            ProductCreatedV1 m => bus.Publish(m, ct),
            ProductUpdatedV1 m => bus.Publish(m, ct),
            ProductPublishedV1 m => bus.Publish(m, ct),
            ProductArchivedV1 m => bus.Publish(m, ct),
            _ => Task.CompletedTask
        };
}
