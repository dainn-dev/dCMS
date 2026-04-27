using dCMS.Core.Messaging;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Catalog.Worker.Imports;

// DAI-707 — consumes ImportJobQueuedV1, streams the file, dispatches each row
// to the matching IImportRowProcessor. Idempotent + resumable via LastProcessedKey.
public sealed class ImportJobConsumer : IConsumer<ImportJobQueuedV1>
{
    private readonly IImportJobPersistence _repo;
    private readonly ImportFileReader _files;
    private readonly IEnumerable<IImportRowProcessor> _processors;
    private readonly ILogger<ImportJobConsumer> _log;

    public ImportJobConsumer(
        IImportJobPersistence repo,
        ImportFileReader files,
        IEnumerable<IImportRowProcessor> processors,
        ILogger<ImportJobConsumer> log)
    {
        _repo = repo;
        _files = files;
        _processors = processors;
        _log = log;
    }

    public async Task Consume(ConsumeContext<ImportJobQueuedV1> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var job = await _repo.GetAsync(msg.JobId, msg.TenantId, ct).ConfigureAwait(false);
        if (job is null)
        {
            _log.LogWarning("ImportJob {JobId} not found for tenant {TenantId}; skipping.", msg.JobId, msg.TenantId);
            return;
        }
        if (job.Status is ImportJobStatuses.Completed or ImportJobStatuses.PartiallyCompleted or ImportJobStatuses.Failed)
        {
            _log.LogInformation("ImportJob {JobId} already terminal ({Status}); skipping.", msg.JobId, job.Status);
            return;
        }

        var processor = _processors.FirstOrDefault(p => p.Type == msg.Type);
        if (processor is null)
        {
            _log.LogError("No processor registered for import type {Type} (job {JobId}).", msg.Type, msg.JobId);
            await _repo.MarkCompletedAsync(msg.JobId, msg.TenantId, ImportJobStatuses.Failed, job.Processed, ct)
                .ConfigureAwait(false);
            return;
        }

        await _repo.MarkRunningAsync(msg.JobId, msg.TenantId, ct).ConfigureAwait(false);

        var resumeAfter = job.LastProcessedKey;
        var resumed = string.IsNullOrEmpty(resumeAfter);
        int processed = job.Processed;
        int errorCount = job.Errors.Count;
        var importCtx = new ImportContext(msg.TenantId, msg.JobId);

        await using var stream = await _files.OpenReadAsync(msg.FileKey, ct).ConfigureAwait(false);

        await foreach (var row in XlsxStreamReader.EnumerateRowsAsync(stream, ct).ConfigureAwait(false))
        {
            if (!resumed)
            {
                if (row.Key == resumeAfter) resumed = true;
                continue;
            }

            try
            {
                var result = await processor.ProcessAsync(row, importCtx, ct).ConfigureAwait(false);
                if (result.IsError)
                {
                    await _repo.AppendErrorAsync(msg.JobId, msg.TenantId,
                        new ImportRowError(row.Index, row.Key, result.Message ?? "unknown error"), ct).ConfigureAwait(false);
                    errorCount++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Row {Index} ({Key}) failed in job {JobId}.", row.Index, row.Key, msg.JobId);
                await _repo.AppendErrorAsync(msg.JobId, msg.TenantId,
                    new ImportRowError(row.Index, row.Key, ex.Message), ct).ConfigureAwait(false);
                errorCount++;
            }

            processed++;
            if (processed % 100 == 0)
                await _repo.UpdateProgressAsync(msg.JobId, msg.TenantId, processed, row.Key, ct).ConfigureAwait(false);
        }

        var finalStatus = errorCount == 0 ? ImportJobStatuses.Completed : ImportJobStatuses.PartiallyCompleted;
        await _repo.MarkCompletedAsync(msg.JobId, msg.TenantId, finalStatus, processed, ct).ConfigureAwait(false);
        _log.LogInformation("ImportJob {JobId} done: {Processed} rows, {Errors} errors, status={Status}.",
            msg.JobId, processed, errorCount, finalStatus);
    }
}
