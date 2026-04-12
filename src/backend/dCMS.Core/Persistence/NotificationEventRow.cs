namespace dCMS.Core.Persistence;

/// <summary>Row from <c>NotificationEvents</c> (catalog DB, migration 009).</summary>
public sealed record NotificationEventRow(long Id, string TenantId, string UserId, string Type, string EntityId, string Message,
    DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);
