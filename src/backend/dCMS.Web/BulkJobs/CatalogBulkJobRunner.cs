using System.Globalization;
using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.ValueObjects;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Web.BulkJobs;

public sealed class CatalogBulkJobRunner
{
    private const int ProgressLogEveryN = 25;

    private readonly ICatalogPersistence _catalog;
    private readonly IBulkJobRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CatalogBulkJobRunner> _log;

    private static readonly JsonSerializerOptions NameJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CatalogBulkJobRunner(
        ICatalogPersistence catalog,
        IBulkJobRepository repo,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<CatalogBulkJobRunner> log)
    {
        _catalog = catalog;
        _repo = repo;
        _configuration = configuration;
        _env = env;
        _log = log;
    }

    [AutomaticRetry(Attempts = 0)]
    [Queue("bulk")]
    [JobDisplayName("dCMS: catalog import")]
    public async Task RunAsync(string tenantId, Guid jobId)
    {
        var now = DateTimeOffset.UtcNow;
        await _repo.MarkStartedAsync(tenantId, jobId, now, default).ConfigureAwait(false);

        try
        {
            var job = await _repo.GetByIdAsync(tenantId, jobId, default).ConfigureAwait(false);
            if (job is null) throw new InvalidOperationException("Job not found.");
            if (string.IsNullOrWhiteSpace(job.InputBlobRef)) throw new InvalidOperationException("Missing input file.");

            var inputPath = BulkJobPathHelper.ResolveUnderContentRoot(_env, job.InputBlobRef);
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file missing.", inputPath);

            var storeId = job.StoreId?.Trim() ?? _configuration["Dcms:Estore:StoreId"]?.Trim() ?? throw new InvalidOperationException("StoreId required.");

            var lines = await File.ReadAllLinesAsync(inputPath, default).ConfigureAwait(false);
            if (lines.Length < 1) throw new InvalidOperationException("CSV is empty.");

            var dataLines = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var total = dataLines.Count;
            var processed = 0;
            await _repo.UpdateProgressAsync(tenantId, jobId, 0, total, 0, default).ConfigureAwait(false);
            BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.CatalogImport).Inc();

            for (var i = 0; i < dataLines.Count; i++)
            {
                if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false)) break;

                var parts = BulkJobText.SplitCsvLine(dataLines[i]);
                if (parts.Count < 3)
                    throw new InvalidOperationException($"Line {i + 2}: expected at least slug, categoryId, nameVi.");

                var slug = parts[0].Trim().ToLowerInvariant();
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId) || categoryId <= 0)
                    throw new InvalidOperationException($"Line {i + 2}: invalid categoryId.");

                var nameVi = parts[2];
                var desc = parts.Count > 3 ? parts[3] : "";

                var cat = await _catalog.GetCategoryByIdAsync(categoryId, tenantId, default).ConfigureAwait(false);
                if (cat is null) throw new InvalidOperationException($"Line {i + 2}: category {categoryId} not found for tenant.");

                var nameJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["vi"] = nameVi }, NameJson);
                var descJson = string.IsNullOrWhiteSpace(desc)
                    ? "{}"
                    : JsonSerializer.Serialize(new Dictionary<string, string> { ["vi"] = desc }, NameJson);

                MultilangJson.ValidateNameRequiredVi(nameJson);
                MultilangJson.ValidateDescriptionOptional(descJson);

                var t = DateTimeOffset.UtcNow;
                var existing = await _catalog.GetBySlugAsync(storeId, tenantId, slug, default).ConfigureAwait(false);
                if (existing is not null)
                {
                    if (existing.Status == ProductStatus.Archived)
                        throw new InvalidOperationException($"Line {i + 2}: product {slug} is archived.");
                    existing.UpdateDetails(categoryId, nameJson, descJson, slug, t);
                    await _catalog.SaveProductWithOutboxAsync(existing, default).ConfigureAwait(false);
                }
                else
                {
                    var p = Product.Create(tenantId, storeId, categoryId, nameJson, descJson, slug, t);
                    await _catalog.SaveProductWithOutboxAsync(p, default).ConfigureAwait(false);
                }

                processed++;
                var percent = total == 0 ? 0 : (int)(100.0 * processed / total);
                if (processed % ProgressLogEveryN == 0 || processed == total)
                {
                    await _repo.UpdateProgressAsync(tenantId, jobId, processed, total, percent, default).ConfigureAwait(false);
                    BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.CatalogImport).Inc();
                }
            }

            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false))
            {
                await _repo.MarkFinishedAsync(tenantId, jobId, "cancelled", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
                BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.CatalogImport, "cancelled").Inc();
                return;
            }

            await _repo.UpdateProgressAsync(tenantId, jobId, processed, total, 100, default).ConfigureAwait(false);
            await _repo.MarkFinishedAsync(tenantId, jobId, "succeeded", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.CatalogImport, "succeeded").Inc();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Catalog import failed for {Tenant} job {JobId}", tenantId, jobId);
            await _repo.MarkFinishedAsync(tenantId, jobId, "failed", DateTimeOffset.UtcNow, ex.Message, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.CatalogImport, "failed").Inc();
            throw;
        }
    }

    private async Task<bool> IsCancelledAsync(string tenantId, Guid jobId, CancellationToken ct)
    {
        var j = await _repo.GetByIdAsync(tenantId, jobId, ct).ConfigureAwait(false);
        return j?.CancelRequestedAt is not null;
    }
}
