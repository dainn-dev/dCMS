using dCMS.Core.Audit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Audit;

public sealed class AuditLogBackgroundService(
    AuditLogChannel channel,
    SqlAuditLogPersistence persistence,
    ILogger<AuditLogBackgroundService> logger) : BackgroundService
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
                        await persistence.InsertAsync(entry, stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Audit log insert failed for {Action} {EntityType}/{EntityId}.", entry.Action,
                            entry.EntityType, entry.EntityId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log background loop error.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
