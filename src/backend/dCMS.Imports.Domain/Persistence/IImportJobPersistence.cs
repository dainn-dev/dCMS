using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

// DAI-684 / DAI-706: tenant-scoped persistence for "ImportJobs".
public interface IImportJobPersistence
{
    Task CreateAsync(ImportJob job, CancellationToken ct = default);

    Task<ImportJob?> GetAsync(string jobId, string tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<ImportJob>> ListRecentAsync(string tenantId, int take, CancellationToken ct = default);

    Task MarkRunningAsync(string jobId, string tenantId, CancellationToken ct = default);

    Task UpdateProgressAsync(string jobId, string tenantId, int processed, string lastProcessedKey,
        CancellationToken ct = default);

    Task AppendErrorAsync(string jobId, string tenantId, ImportRowError error, CancellationToken ct = default);

    Task MarkCompletedAsync(string jobId, string tenantId, string finalStatus, int processed,
        CancellationToken ct = default);
}
