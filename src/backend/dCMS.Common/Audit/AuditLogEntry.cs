namespace dCMS.Core.Audit;

/// <summary>US-11: append-only audit row (matches <c>AuditLogs</c> migration 009).</summary>
public sealed record AuditLogEntry(
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
