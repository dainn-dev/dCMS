namespace dCMS.Order.Infrastructure.Persistence;

public sealed class OrderFailureRow
{
    public Guid OrderId { get; init; }
    public string TenantId { get; init; } = null!;
    public string StoreId { get; init; } = null!;
    public string FailureStatus { get; init; } = null!;
    public string FailureReason { get; init; } = null!;
    public string? FailureErrorCode { get; init; }
    public string? SourceEventId { get; init; }
    public DateTimeOffset FailedAt { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset? LastRetryAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public string LogJson { get; init; } = "[]";
}

