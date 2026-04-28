using dCMS.Core.Audit;
using dCMS.Core.Messaging;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Audit;

/// <summary>
/// Phase C: drains <see cref="AuditLogChannel"/> by publishing <see cref="AuditLogQueuedV1"/> over MassTransit
/// instead of writing directly to dcms_catalog. Used by services that don't own the catalog DB.
/// </summary>
public sealed class PublishAuditLogBackgroundService(
    AuditLogChannel channel,
    IBus bus,
    ILogger<PublishAuditLogBackgroundService> logger) : BackgroundService
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
                        await bus.Publish(ToContract(entry), stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex,
                            "Audit log publish failed for {Action} {EntityType}/{EntityId}.",
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
                logger.LogError(ex, "Audit log publish loop error.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static AuditLogQueuedV1 ToContract(AuditLogEntry e) => new(
        e.TenantId, e.StoreId, e.UserId, e.UserRole, e.Action, e.EntityType, e.EntityId, e.Diff, e.IpAddress,
        e.CreatedAt);
}
