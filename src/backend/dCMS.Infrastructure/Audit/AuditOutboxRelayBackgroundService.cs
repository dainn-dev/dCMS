using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Audit;

/// <summary>
/// Polls <c>AuditOutbox</c> and publishes pending <c>AuditLogQueuedV1</c> rows over MassTransit.
/// At-least-once: rows are only marked processed after a successful publish. Failures bump RetryCount.
/// </summary>
public sealed class AuditOutboxRelayBackgroundService(
    AuditOutboxPersistence outbox,
    IBus bus,
    ILogger<AuditOutboxRelayBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await outbox.ProcessPendingAsync(
                    msg => bus.Publish(msg, stoppingToken),
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit outbox relay iteration failed.");
            }

            try
            {
                await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
