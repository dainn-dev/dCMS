namespace dCMS.Core.Persistence;

/// <summary>Row from <c>NotificationEvents</c> (catalog DB, migration 009).</summary>
public sealed class NotificationEventRow
{
    public long Id { get; init; }
    public string TenantId { get; init; } = null!;
    public string UserId { get; init; } = null!;
    public string Type { get; init; } = null!;
    public string EntityId { get; init; } = null!;
    public string Message { get; init; } = null!;
    public DateTimeOffset? ReadAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
