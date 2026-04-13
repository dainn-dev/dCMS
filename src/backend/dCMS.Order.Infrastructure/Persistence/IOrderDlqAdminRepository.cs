namespace dCMS.Order.Infrastructure.Persistence;

public interface IOrderDlqAdminRepository
{
    Task<IReadOnlyList<OrderDlqListItem>> ListAsync(
        string? eventType,
        DateTimeOffset? failedFrom,
        DateTimeOffset? failedTo,
        CancellationToken cancellationToken = default);

    Task<OrderDlqRow?> GetAsync(long deadLetterId, CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(long deadLetterId, CancellationToken cancellationToken = default);

    Task<bool> DiscardAsync(long deadLetterId, string reason, CancellationToken cancellationToken = default);
}

public sealed record OrderDlqListItem(
    long Id,
    string? OrderId,
    string EventType,
    string FailureReason,
    DateTimeOffset FailedAt,
    int SourceRetryCount,
    DateTimeOffset? ReprocessedAt,
    DateTimeOffset? DiscardedAt);

public sealed class OrderDlqRow
{
    public long Id { get; init; }
    public long? SourceOutboxId { get; init; }
    public string EventType { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public string FailureReason { get; init; } = null!;
    public DateTimeOffset FailedAt { get; init; }
    public DateTimeOffset? ReprocessedAt { get; init; }
    public DateTimeOffset? DiscardedAt { get; init; }
}
