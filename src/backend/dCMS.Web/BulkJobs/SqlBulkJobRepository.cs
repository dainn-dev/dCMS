using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace dCMS.Web.BulkJobs;

public sealed class SqlBulkJobRepository : IBulkJobRepository
{
    private readonly string _cs;

    public SqlBulkJobRepository(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("umbracoDbDSN")
            ?? throw new InvalidOperationException("ConnectionStrings:umbracoDbDSN is required.");
    }

    public async Task InsertAsync(BulkJobRecord row, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dcms_bulk_jobs (
                id, tenant_id, store_id, job_kind, requested_by_user, hangfire_job_id, status,
                progress_processed, progress_total, progress_percent,
                input_blob_ref, output_blob_ref, error_message,
                created_at, started_at, finished_at, cancel_requested_at)
            VALUES (
                @Id, @TenantId, @StoreId, @JobKind, @RequestedByUserId, @HangfireJobId, @Status,
                @ProgressProcessed, @ProgressTotal, @ProgressPercent,
                @InputBlobRef, @OutputBlobRef, @ErrorMessage,
                @CreatedAt, @StartedAt, @FinishedAt, @CancelRequestedAt)
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                row.Id,
                TenantId = row.TenantId,
                StoreId = row.StoreId,
                JobKind = row.JobKind,
                RequestedByUserId = row.RequestedByUserId,
                row.HangfireJobId,
                row.Status,
                row.ProgressProcessed,
                row.ProgressTotal,
                row.ProgressPercent,
                row.InputBlobRef,
                row.OutputBlobRef,
                row.ErrorMessage,
                row.CreatedAt,
                StartedAt = row.StartedAt,
                FinishedAt = row.FinishedAt,
                row.CancelRequestedAt,
            }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<BulkJobRecord?> GetByIdAsync(string tenantId, Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                store_id AS StoreId,
                job_kind AS JobKind,
                requested_by_user AS RequestedByUserId,
                hangfire_job_id AS HangfireJobId,
                status AS Status,
                progress_processed AS ProgressProcessed,
                progress_total AS ProgressTotal,
                progress_percent AS ProgressPercent,
                input_blob_ref AS InputBlobRef,
                output_blob_ref AS OutputBlobRef,
                error_message AS ErrorMessage,
                created_at AS CreatedAt,
                started_at AS StartedAt,
                finished_at AS FinishedAt,
                cancel_requested_at AS CancelRequestedAt
            FROM dcms_bulk_jobs
            WHERE tenant_id = @TenantId AND id = @Id
            """;
        await using var conn = new SqlConnection(_cs);
        return await conn.QuerySingleOrDefaultAsync<BulkJobRecord>(
            new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BulkJobRecord>> ListByTenantAsync(string tenantId, int limit, CancellationToken ct = default)
    {
        var lim = Math.Clamp(limit, 1, 200);
        const string sql = """
            SELECT TOP (@Limit)
                id AS Id,
                tenant_id AS TenantId,
                store_id AS StoreId,
                job_kind AS JobKind,
                requested_by_user AS RequestedByUserId,
                hangfire_job_id AS HangfireJobId,
                status AS Status,
                progress_processed AS ProgressProcessed,
                progress_total AS ProgressTotal,
                progress_percent AS ProgressPercent,
                input_blob_ref AS InputBlobRef,
                output_blob_ref AS OutputBlobRef,
                error_message AS ErrorMessage,
                created_at AS CreatedAt,
                started_at AS StartedAt,
                finished_at AS FinishedAt,
                cancel_requested_at AS CancelRequestedAt
            FROM dcms_bulk_jobs
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            """;
        await using var conn = new SqlConnection(_cs);
        var rows = await conn.QueryAsync<BulkJobRecord>(
            new CommandDefinition(sql, new { TenantId = tenantId, Limit = lim }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SetHangfireJobIdAsync(string tenantId, Guid id, string hangfireJobId, CancellationToken ct = default)
    {
        const string sql = "UPDATE dcms_bulk_jobs SET hangfire_job_id = @Hj WHERE tenant_id = @TenantId AND id = @Id";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Hj = hangfireJobId, TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task UpdateStatusAsync(string tenantId, Guid id, string status, CancellationToken ct = default)
    {
        const string sql = "UPDATE dcms_bulk_jobs SET status = @St WHERE tenant_id = @TenantId AND id = @Id";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { St = status, TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task MarkStartedAsync(string tenantId, Guid id, DateTimeOffset at, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dcms_bulk_jobs
            SET status = 'running', started_at = @At
            WHERE tenant_id = @TenantId AND id = @Id
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { At = at, TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task MarkFinishedAsync(
        string tenantId, Guid id, string status, DateTimeOffset at, string? errorMessage, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dcms_bulk_jobs
            SET status = @St, finished_at = @At, error_message = @Err
            WHERE tenant_id = @TenantId AND id = @Id
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            St = status,
            At = at,
            Err = (string?)errorMessage,
            TenantId = tenantId,
            Id = id,
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpdateProgressAsync(
        string tenantId, Guid id, int processed, int total, int percent, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dcms_bulk_jobs
            SET progress_processed = @P, progress_total = @T, progress_percent = @Pc
            WHERE tenant_id = @TenantId AND id = @Id
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            sql, new { P = processed, T = total, Pc = percent, TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task SetOutputBlobRefAsync(string tenantId, Guid id, string refPath, CancellationToken ct = default)
    {
        const string sql = "UPDATE dcms_bulk_jobs SET output_blob_ref = @O WHERE tenant_id = @TenantId AND id = @Id";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { O = refPath, TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task SetCancelRequestedAsync(string tenantId, Guid id, DateTimeOffset at, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dcms_bulk_jobs
            SET cancel_requested_at = @At
            WHERE tenant_id = @TenantId AND id = @Id
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { At = at, TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> ResetFailedJobForRetryAsync(string tenantId, Guid id, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dcms_bulk_jobs
            SET status = 'queued', error_message = NULL, finished_at = NULL, hangfire_job_id = NULL,
                cancel_requested_at = NULL, started_at = NULL,
                progress_processed = 0, progress_total = 0, progress_percent = 0, output_blob_ref = NULL
            WHERE tenant_id = @TenantId AND id = @Id AND status = 'failed'
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
