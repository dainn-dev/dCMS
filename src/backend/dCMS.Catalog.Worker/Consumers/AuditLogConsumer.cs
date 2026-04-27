using dCMS.Core.Audit;
using dCMS.Core.Messaging;
using dCMS.Infrastructure.Audit;
using MassTransit;

namespace dCMS.Catalog.Worker.Consumers;

/// <summary>
/// Phase C: persists audit rows published by services that don't own dcms_catalog
/// (Promotions.Api, Fulfillment.Api). Catalog.Api still writes directly since it owns the DB.
/// </summary>
public sealed class AuditLogConsumer(SqlAuditLogPersistence persistence)
    : IConsumer<AuditLogQueuedV1>
{
    public Task Consume(ConsumeContext<AuditLogQueuedV1> context)
    {
        var m = context.Message;
        var entry = new AuditLogEntry(
            m.TenantId, m.StoreId, m.UserId, m.UserRole, m.Action, m.EntityType, m.EntityId, m.Diff,
            m.IpAddress, m.CreatedAt);
        return persistence.InsertAsync(entry, context.CancellationToken);
    }
}
