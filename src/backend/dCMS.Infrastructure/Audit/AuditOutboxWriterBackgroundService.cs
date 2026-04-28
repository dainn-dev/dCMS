using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Audit;

/// <summary>
/// Drains <see cref="AuditLogChannel"/> by inserting each entry into the local <c>AuditOutbox</c> table.
/// Pairs with <see cref="AuditOutboxRelayBackgroundService"/> which polls the outbox and publishes to the bus
/// (at-least-once: rows survive process restart).
/// </summary>
public sealed class AuditOutboxWriterBackgroundService(
    AuditLogChannel channel,
    AuditOutboxPersistence outbox,
    ILogger<AuditOutboxWriterBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var entry in channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    try
                    {
                        await outbox.InsertAsync(entry, stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex,
                            "Audit outbox insert failed for {Action} {EntityType}/{EntityId}.",
                            entry.Action, entry.EntityType, entry.EntityId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit outbox writer loop error.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
