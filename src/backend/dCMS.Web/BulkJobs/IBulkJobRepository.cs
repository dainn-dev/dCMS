namespace dCMS.Web.BulkJobs;

public interface IBulkJobRepository
{
    Task InsertAsync(BulkJobRecord row, CancellationToken ct = default);
    Task<BulkJobRecord?> GetByIdAsync(string tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BulkJobRecord>> ListByTenantAsync(string tenantId, int limit, CancellationToken ct = default);
    Task SetHangfireJobIdAsync(string tenantId, Guid id, string hangfireJobId, CancellationToken ct = default);
    Task UpdateStatusAsync(string tenantId, Guid id, string status, CancellationToken ct = default);
    Task MarkStartedAsync(string tenantId, Guid id, DateTimeOffset at, CancellationToken ct = default);
    Task MarkFinishedAsync(string tenantId, Guid id, string status, DateTimeOffset at, string? errorMessage, CancellationToken ct = default);
    Task UpdateProgressAsync(string tenantId, Guid id, int processed, int total, int percent, CancellationToken ct = default);
    Task SetOutputBlobRefAsync(string tenantId, Guid id, string refPath, CancellationToken ct = default);
    Task SetCancelRequestedAsync(string tenantId, Guid id, DateTimeOffset at, CancellationToken ct = default);
    Task<int> ResetFailedJobForRetryAsync(string tenantId, Guid id, CancellationToken ct = default);
}
