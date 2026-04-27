using System.Text;
using dCMS.Catalog.Worker.Imports;
using dCMS.Core.Messaging;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace dCMS.Tests.Unit.Catalog.Imports;

public class ImportJobConsumerResumeTests : IDisposable
{
    private readonly string _tempRoot;

    public ImportJobConsumerResumeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "dcms-import-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task Resumes_processing_after_LastProcessedKey()
    {
        const string tenantId = "t1";
        const string jobId = "imp_resume1";
        const string fileKey = "imports/t1/imp_resume1/source.csv";
        var csv = "sku,name\nA-1,One\nA-2,Two\nA-3,Three\nA-4,Four\n";

        var fullPath = Path.Combine(_tempRoot, fileKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, Encoding.UTF8.GetBytes(csv));

        var job = new ImportJob(
            Id: jobId, TenantId: tenantId, Type: ImportJobTypes.Products,
            Status: ImportJobStatuses.Running, FileKey: fileKey, Total: 4,
            Processed: 2, Errors: Array.Empty<ImportRowError>(), LastProcessedKey: "A-2",
            CreatedBy: "test", CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: null);

        var repo = new FakeImportJobPersistence(job);
        var processor = new RecordingProcessor(ImportJobTypes.Products);
        var reader = BuildReader();
        var consumer = new ImportJobConsumer(
            repo, reader, new[] { (IImportRowProcessor)processor }, NullLogger<ImportJobConsumer>.Instance);

        var ctx = new Mock<ConsumeContext<ImportJobQueuedV1>>();
        ctx.SetupGet(c => c.Message).Returns(
            new ImportJobQueuedV1(jobId, tenantId, ImportJobTypes.Products, fileKey, DateTimeOffset.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        // Only A-3 and A-4 should be processed (A-1, A-2 already committed before the crash).
        processor.Processed.Select(r => r.Key).Should().Equal("A-3", "A-4");
        repo.FinalStatus.Should().Be(ImportJobStatuses.Completed);
        repo.FinalProcessed.Should().Be(4);
    }

    [Fact]
    public async Task Skips_consume_when_job_is_already_terminal()
    {
        const string tenantId = "t1";
        const string jobId = "imp_done1";
        var fileKey = "imports/t1/imp_done1/source.csv";
        // Note: file does not exist — proves we do not even open it.

        var job = new ImportJob(
            Id: jobId, TenantId: tenantId, Type: ImportJobTypes.Products,
            Status: ImportJobStatuses.Completed, FileKey: fileKey, Total: 1,
            Processed: 1, Errors: Array.Empty<ImportRowError>(), LastProcessedKey: "A-1",
            CreatedBy: "test", CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow);

        var repo = new FakeImportJobPersistence(job);
        var processor = new RecordingProcessor(ImportJobTypes.Products);
        var reader = BuildReader();
        var consumer = new ImportJobConsumer(
            repo, reader, new[] { (IImportRowProcessor)processor }, NullLogger<ImportJobConsumer>.Instance);

        var ctx = new Mock<ConsumeContext<ImportJobQueuedV1>>();
        ctx.SetupGet(c => c.Message).Returns(
            new ImportJobQueuedV1(jobId, tenantId, ImportJobTypes.Products, fileKey, DateTimeOffset.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        processor.Processed.Should().BeEmpty();
        repo.MarkRunningCalls.Should().Be(0);
    }

    [Fact]
    public async Task Records_row_errors_and_marks_partially_completed()
    {
        const string tenantId = "t1";
        const string jobId = "imp_err1";
        var fileKey = "imports/t1/imp_err1/source.csv";
        var csv = "sku,name\nGOOD-1,Ok\nBAD-1,Bad\nGOOD-2,Ok\n";

        var fullPath = Path.Combine(_tempRoot, fileKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, Encoding.UTF8.GetBytes(csv));

        var job = new ImportJob(
            Id: jobId, TenantId: tenantId, Type: ImportJobTypes.Products,
            Status: ImportJobStatuses.Pending, FileKey: fileKey, Total: 3,
            Processed: 0, Errors: Array.Empty<ImportRowError>(), LastProcessedKey: null,
            CreatedBy: "test", CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: null, CompletedAt: null);

        var repo = new FakeImportJobPersistence(job);
        var processor = new RecordingProcessor(ImportJobTypes.Products,
            r => r.Key.StartsWith("BAD") ? RowResult.Err("nope") : RowResult.Ok);
        var reader = BuildReader();
        var consumer = new ImportJobConsumer(
            repo, reader, new[] { (IImportRowProcessor)processor }, NullLogger<ImportJobConsumer>.Instance);

        var ctx = new Mock<ConsumeContext<ImportJobQueuedV1>>();
        ctx.SetupGet(c => c.Message).Returns(
            new ImportJobQueuedV1(jobId, tenantId, ImportJobTypes.Products, fileKey, DateTimeOffset.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        repo.AppendedErrors.Should().HaveCount(1);
        repo.AppendedErrors[0].Key.Should().Be("BAD-1");
        repo.FinalStatus.Should().Be(ImportJobStatuses.PartiallyCompleted);
        repo.FinalProcessed.Should().Be(3);
    }

    [Fact]
    public async Task Marks_failed_when_no_processor_registered_for_type()
    {
        const string tenantId = "t1";
        const string jobId = "imp_unknown";
        var fileKey = "imports/t1/imp_unknown/source.csv";

        var job = new ImportJob(
            Id: jobId, TenantId: tenantId, Type: "unknown-type",
            Status: ImportJobStatuses.Pending, FileKey: fileKey, Total: null,
            Processed: 0, Errors: Array.Empty<ImportRowError>(), LastProcessedKey: null,
            CreatedBy: "test", CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: null, CompletedAt: null);

        var repo = new FakeImportJobPersistence(job);
        var processor = new RecordingProcessor(ImportJobTypes.Products);
        var reader = BuildReader();
        var consumer = new ImportJobConsumer(
            repo, reader, new[] { (IImportRowProcessor)processor }, NullLogger<ImportJobConsumer>.Instance);

        var ctx = new Mock<ConsumeContext<ImportJobQueuedV1>>();
        ctx.SetupGet(c => c.Message).Returns(
            new ImportJobQueuedV1(jobId, tenantId, "unknown-type", fileKey, DateTimeOffset.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        repo.FinalStatus.Should().Be(ImportJobStatuses.Failed);
        processor.Processed.Should().BeEmpty();
    }

    private ImportFileReader BuildReader()
    {
        var readerOpts = Options.Create(new ImportFileReaderOptions()); // no S3 — fall back to local
        var mediaOpts = Options.Create(new CatalogMediaPathOptions { RootPath = _tempRoot });
        var env = new FakeHostEnvironment(_tempRoot);
        return new ImportFileReader(readerOpts, mediaOpts, env, NullLogger<ImportFileReader>.Instance);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string contentRoot)
        {
            ContentRootPath = contentRoot;
            ContentRootFileProvider = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "dCMS.Tests";
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class RecordingProcessor : IImportRowProcessor
    {
        private readonly Func<ImportRow, RowResult> _result;
        public List<ImportRow> Processed { get; } = new();

        public RecordingProcessor(string type, Func<ImportRow, RowResult>? result = null)
        {
            Type = type;
            _result = result ?? (_ => RowResult.Ok);
        }

        public string Type { get; }

        public Task<RowResult> ProcessAsync(ImportRow row, ImportContext ctx, CancellationToken ct)
        {
            Processed.Add(row);
            return Task.FromResult(_result(row));
        }
    }

    private sealed class FakeImportJobPersistence : IImportJobPersistence
    {
        private ImportJob _job;
        public List<ImportRowError> AppendedErrors { get; } = new();
        public int MarkRunningCalls { get; private set; }
        public string? FinalStatus { get; private set; }
        public int FinalProcessed { get; private set; }

        public FakeImportJobPersistence(ImportJob job) { _job = job; }

        public Task CreateAsync(ImportJob job, CancellationToken ct = default)
        {
            _job = job;
            return Task.CompletedTask;
        }

        public Task<ImportJob?> GetAsync(string jobId, string tenantId, CancellationToken ct = default)
            => Task.FromResult<ImportJob?>(_job);

        public Task<IReadOnlyList<ImportJob>> ListRecentAsync(string tenantId, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ImportJob>>(new[] { _job });

        public Task MarkRunningAsync(string jobId, string tenantId, CancellationToken ct = default)
        {
            MarkRunningCalls++;
            _job = _job with { Status = ImportJobStatuses.Running, StartedAt = DateTimeOffset.UtcNow };
            return Task.CompletedTask;
        }

        public Task UpdateProgressAsync(string jobId, string tenantId, int processed, string lastProcessedKey,
            CancellationToken ct = default)
        {
            _job = _job with { Processed = processed, LastProcessedKey = lastProcessedKey };
            return Task.CompletedTask;
        }

        public Task AppendErrorAsync(string jobId, string tenantId, ImportRowError error, CancellationToken ct = default)
        {
            AppendedErrors.Add(error);
            var nextErrors = AppendedErrors.ToArray();
            _job = _job with { Errors = nextErrors };
            return Task.CompletedTask;
        }

        public Task MarkCompletedAsync(string jobId, string tenantId, string finalStatus, int processed,
            CancellationToken ct = default)
        {
            FinalStatus = finalStatus;
            FinalProcessed = processed;
            _job = _job with { Status = finalStatus, Processed = processed, CompletedAt = DateTimeOffset.UtcNow };
            return Task.CompletedTask;
        }
    }
}
