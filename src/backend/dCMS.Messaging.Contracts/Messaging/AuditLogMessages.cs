namespace dCMS.Core.Messaging;

/// <summary>
/// Phase C: services that don't own dcms_catalog publish this when they would otherwise
/// have inserted a row into <c>AuditLogs</c>. dCMS.Catalog.Worker consumes and writes to dcms_catalog.
/// </summary>
public sealed record AuditLogQueuedV1(
    string TenantId,
    string StoreId,
    string UserId,
    string UserRole,
    string Action,
    string EntityType,
    string EntityId,
    string? Diff,
    string IpAddress,
    DateTimeOffset CreatedAt);
