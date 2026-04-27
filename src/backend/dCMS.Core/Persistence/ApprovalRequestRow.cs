namespace dCMS.Core.Persistence;

public sealed class ApprovalRequestRow
{
    public required Guid Id { get; init; }
    public required string TenantId { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string State { get; init; }
    public required string RequestedBy { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public string? CurrentApprover { get; init; }
    public required string HistoryJson { get; init; }
    public required string PayloadSnapshotJson { get; init; }
    public DateTimeOffset? FinalizedAt { get; init; }
}

