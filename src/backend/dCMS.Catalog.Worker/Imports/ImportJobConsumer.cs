using dCMS.Core.Messaging;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Infrastructure.Web;
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
            DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "import_job", "skipped", "job_not_found");
            _log.LogWarning(
                "Worker operation skipped service {Service} operation {Operation} status {Status} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} job {JobId}",
                "catalog-worker",
                "import_job",
                "skipped",
                "job_not_found",
                context.CorrelationId?.ToString() ?? context.MessageId?.ToString() ?? "unknown",
                msg.TenantId,
                msg.JobId);
            return;
        }
        if (job.Status is ImportJobStatuses.Completed or ImportJobStatuses.PartiallyCompleted or ImportJobStatuses.Failed)
        {
            DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "import_job", "skipped", "already_terminal");
            _log.LogInformation(
                "Worker operation skipped service {Service} operation {Operation} status {Status} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} job {JobId} jobStatus {JobStatus}",
                "catalog-worker",
                "import_job",
                "skipped",
                "already_terminal",
                context.CorrelationId?.ToString() ?? context.MessageId?.ToString() ?? "unknown",
                msg.TenantId,
                msg.JobId,
                job.Status);
            return;
        }

        var processor = _processors.FirstOrDefault(p => p.Type == msg.Type);
        if (processor is null)
        {
            DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "import_job", "failed", "processor_missing");
            _log.LogError(
                "Worker operation failed service {Service} operation {Operation} status {Status} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} job {JobId} importType {ImportType}",
                "catalog-worker",
                "import_job",
                "failed",
                "processor_missing",
                context.CorrelationId?.ToString() ?? context.MessageId?.ToString() ?? "unknown",
                msg.TenantId,
                msg.JobId,
                msg.Type);
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
                DcmsObservabilityMetrics.ObserveWorkerOperation("catalog-worker", "import_row", "failed", "row_processor_error");
                _log.LogError(
                    ex,
                    "Worker operation failed service {Service} operation {Operation} status {Status} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} job {JobId} row {RowIndex}",
                    "catalog-worker",
                    "import_row",
                    "failed",
                    "row_processor_error",
                    context.CorrelationId?.ToString() ?? context.MessageId?.ToString() ?? "unknown",
                    msg.TenantId,
                    msg.JobId,
                    row.Index);
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
        DcmsObservabilityMetrics.ObserveWorkerOperation(
            "catalog-worker",
            "import_job",
            errorCount == 0 ? "succeeded" : "partial",
            errorCount == 0 ? "none" : "row_errors");
        _log.LogInformation(
            "Worker operation completed service {Service} operation {Operation} status {Status} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} job {JobId} processed {Processed} errors {Errors}",
            "catalog-worker",
            "import_job",
            errorCount == 0 ? "succeeded" : "partial",
            errorCount == 0 ? "none" : "row_errors",
            context.CorrelationId?.ToString() ?? context.MessageId?.ToString() ?? "unknown",
            msg.TenantId,
            msg.JobId,
            processed,
            errorCount);
    }
}
