namespace dCMS.Core.Persistence;

public interface IApprovalRequestPersistence
{
    Task<Guid> CreatePendingAsync(
        string tenantId,
        string entityType,
        string entityId,
        string requestedByUserId,
        string? currentApproverUserId,
        string payloadSnapshotJson,
        DateTimeOffset now,
        CancellationToken ct);

    Task<ApprovalRequestRow?> GetByIdAsync(string tenantId, Guid id, CancellationToken ct);

    Task<ApprovalRequestRow?> GetByEntityAsync(string tenantId, string entityType, string entityId, CancellationToken ct);

    Task<(IReadOnlyList<ApprovalRequestRow> Items, int Total)> ListAsync(
        string tenantId,
        string? entityType,
        string? state,
        string? assignedTo,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<bool> TryTransitionAsync(
        string tenantId,
        Guid id,
        string expectedState,
        string nextState,
        string actedByUserId,
        string? notes,
        DateTimeOffset now,
        bool finalize,
        CancellationToken ct);

    Task<int> BulkApproveAsync(string tenantId, IReadOnlyList<Guid> ids, string actedByUserId, DateTimeOffset now, CancellationToken ct);
}

