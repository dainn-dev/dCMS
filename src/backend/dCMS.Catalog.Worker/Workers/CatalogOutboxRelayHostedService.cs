using dCMS.Core.Messaging;
using dCMS.Infrastructure.Web;
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
                await relay.ProcessPendingAsync((_, m) => PublishAsync(m, stoppingToken), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "outbox_relay", "failed", "relay_iteration_failed");
                log.LogError(
                    ex,
                    "Worker operation failed service {Service} operation {Operation} status {Status} failure {FailureReason}",
                    "catalog-worker",
                    "outbox_relay",
                    "failed",
                    "relay_iteration_failed");
            }

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PublishAsync(object message, CancellationToken ct)
    {
        var eventType = message.GetType().Name;
        try
        {
            switch (message)
            {
                case ProductCreatedV1 m:
                    await bus.Publish(m, ct).ConfigureAwait(false);
                    break;
                case ProductUpdatedV1 m:
                    await bus.Publish(m, ct).ConfigureAwait(false);
                    break;
                case ProductPublishedV1 m:
                    await bus.Publish(m, ct).ConfigureAwait(false);
                    break;
                case ProductArchivedV1 m:
                    await bus.Publish(m, ct).ConfigureAwait(false);
                    break;
                default:
                    DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "outbox_publish", "skipped", "unsupported_event");
                    log.LogWarning(
                        "Worker operation skipped service {Service} operation {Operation} status {Status} failure {FailureReason} eventType {EventType}",
                        "catalog-worker",
                        "outbox_publish",
                        "skipped",
                        "unsupported_event",
                        eventType);
                    return;
            }

            DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "outbox_publish", "succeeded");
            log.LogInformation(
                "Worker operation completed service {Service} operation {Operation} status {Status} failure {FailureReason} eventType {EventType}",
                "catalog-worker",
                "outbox_publish",
                "succeeded",
                "none",
                eventType);
        }
        catch (Exception ex)
        {
            DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "outbox_publish", "failed", "publish_failed");
            log.LogError(
                ex,
                "Worker operation failed service {Service} operation {Operation} status {Status} failure {FailureReason} eventType {EventType}",
                "catalog-worker",
                "outbox_publish",
                "failed",
                "publish_failed",
                eventType);
            throw;
        }
    }
}
