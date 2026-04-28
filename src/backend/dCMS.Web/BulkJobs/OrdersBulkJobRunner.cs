using System.Text;
using Dapper;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Web.BulkJobs;

public sealed class OrdersBulkJobRunner
{
    private const int ProgressLogEveryN = 25;

    private readonly IBulkJobRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<OrdersBulkJobRunner> _log;

    public OrdersBulkJobRunner(
        IBulkJobRepository repo,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<OrdersBulkJobRunner> log)
    {
        _repo = repo;
        _configuration = configuration;
        _env = env;
        _log = log;
    }

    [AutomaticRetry(Attempts = 0)]
    [Queue("bulk")]
    [JobDisplayName("dCMS: orders export")]
    public async Task RunAsync(string tenantId, Guid jobId)
    {
        var now = DateTimeOffset.UtcNow;
        await _repo.MarkStartedAsync(tenantId, jobId, now, default).ConfigureAwait(false);

        var orderCs = _configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is not configured for orders export.");

        try
        {
            var job = await _repo.GetByIdAsync(tenantId, jobId, default).ConfigureAwait(false);
            if (job is null) throw new InvalidOperationException("Job not found.");
            if (string.IsNullOrWhiteSpace(job.InputBlobRef)) throw new InvalidOperationException("Missing export parameters file.");

            var inputPath = BulkJobPathHelper.ResolveUnderContentRoot(_env, job.InputBlobRef);
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Input params missing.", inputPath);

            var json = await File.ReadAllTextAsync(inputPath, default).ConfigureAwait(false);
            var export = System.Text.Json.JsonSerializer.Deserialize<OrdersExportRequest>(json) ?? throw new InvalidOperationException("Invalid export JSON.");

            var outPath = BulkJobFileStorage.GetOutputFilePath(_env.ContentRootPath, tenantId, jobId);
            BulkJobFileStorage.EnsureJobDir(_env.ContentRootPath, tenantId, jobId);

            var storeFilter = (export.StoreId ?? job.StoreId ?? "").Trim();
            if (export.DateTo < export.DateFrom) throw new InvalidOperationException("dateTo must be >= dateFrom.");
            var dateFrom = export.DateFrom;
            var dateTo = export.DateTo;

            var fromUtc = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var toExclusive = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            await using var conn = new NpgsqlConnection(orderCs);
            await conn.OpenAsync(default).ConfigureAwait(false);

            const string countSql = """
                SELECT COUNT(*)::bigint
                FROM "Orders" o
                WHERE o."TenantId" = @TenantId
                  AND (@StoreId = '' OR o."StoreId" = @StoreId)
                  AND o."CreatedAt" >= @From
                  AND o."CreatedAt" < @ToEx
                """;
            var total = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                countSql, new
                {
                    TenantId = tenantId,
                    StoreId = storeFilter,
                    From = fromUtc,
                    ToEx = toExclusive,
                })).ConfigureAwait(false);

            await _repo.UpdateProgressAsync(tenantId, jobId, 0, (int)Math.Min(total, int.MaxValue), 0, default).ConfigureAwait(false);
            BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.OrdersExport).Inc();

            await using var sw = new StreamWriter(new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
            await sw.WriteLineAsync("Id,TenantId,StoreId,CustomerId,Status,Currency,SubTotal,TaxTotal,Total,CreatedAt").ConfigureAwait(false);

            const string pageSql = """
                SELECT o."Id"::text, o."TenantId", o."StoreId", o."CustomerId", o."Status", o."Currency",
                       o."SubTotal"::text, o."TaxTotal"::text, o."Total"::text, o."CreatedAt"::text
                FROM "Orders" o
                WHERE o."TenantId" = @TenantId
                  AND (@StoreId = '' OR o."StoreId" = @StoreId)
                  AND o."CreatedAt" >= @From
                  AND o."CreatedAt" < @ToEx
                ORDER BY o."CreatedAt" DESC, o."Id" DESC
                LIMIT @Take OFFSET @Off
                """;

            var processed = 0L;
            const int take = 500;
            for (var off = 0; (long)off < total; off += take)
            {
                if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false)) break;

                var rows = await conn.QueryAsync<OrderExportRow>(new CommandDefinition(
                    pageSql, new
                    {
                        TenantId = tenantId,
                        StoreId = storeFilter,
                        From = fromUtc,
                        ToEx = toExclusive,
                        Take = take,
                        Off = off,
                    })).ConfigureAwait(false);
                var list = rows.ToList();
                if (list.Count == 0) break;

                foreach (var r in list)
                {
                    await sw.WriteLineAsync(string.Join(
                        ',',
                        BulkJobText.Csv(r.Id), BulkJobText.Csv(r.TenantId), BulkJobText.Csv(r.StoreId), BulkJobText.Csv(r.CustomerId), BulkJobText.Csv(r.Status),
                        BulkJobText.Csv(r.Currency), BulkJobText.Csv(r.SubTotal), BulkJobText.Csv(r.TaxTotal), BulkJobText.Csv(r.Total), BulkJobText.Csv(r.CreatedAt)))
                        .ConfigureAwait(false);
                }

                processed += list.Count;
                var p = (int)Math.Min(processed, int.MaxValue);
                var t = (int)Math.Min(total, int.MaxValue);
                var percent = t == 0 ? 100 : (int)(100.0 * processed / total);
                if (processed % (ProgressLogEveryN * 10) == 0 || processed >= total)
                {
                    await _repo.UpdateProgressAsync(tenantId, jobId, p, t, percent, default).ConfigureAwait(false);
                    BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.OrdersExport).Inc();
                }
            }

            var rel = Path.Combine("umbraco", "Data", "BulkJobFiles", tenantId, jobId.ToString("N"), "export.csv");
            await _repo.SetOutputBlobRefAsync(tenantId, jobId, rel, default).ConfigureAwait(false);

            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false))
            {
                await _repo.MarkFinishedAsync(tenantId, jobId, "cancelled", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
                BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.OrdersExport, "cancelled").Inc();
                return;
            }

            await _repo.UpdateProgressAsync(
                tenantId, jobId, (int)Math.Min(processed, int.MaxValue), (int)Math.Min(total, int.MaxValue), 100, default).ConfigureAwait(false);
            await _repo.MarkFinishedAsync(tenantId, jobId, "succeeded", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.OrdersExport, "succeeded").Inc();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Orders export failed for {Tenant} job {JobId}", tenantId, jobId);
            await _repo.MarkFinishedAsync(tenantId, jobId, "failed", DateTimeOffset.UtcNow, ex.Message, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.OrdersExport, "failed").Inc();
            throw;
        }
    }

    private async Task<bool> IsCancelledAsync(string tenantId, Guid jobId, CancellationToken ct)
    {
        var j = await _repo.GetByIdAsync(tenantId, jobId, ct).ConfigureAwait(false);
        return j?.CancelRequestedAt is not null;
    }

    private sealed class OrderExportRow
    {
        public string? Id { get; set; }
        public string? TenantId { get; set; }
        public string? StoreId { get; set; }
        public string? CustomerId { get; set; }
        public string? Status { get; set; }
        public string? Currency { get; set; }
        public string? SubTotal { get; set; }
        public string? TaxTotal { get; set; }
        public string? Total { get; set; }
        public string? CreatedAt { get; set; }
    }
}
