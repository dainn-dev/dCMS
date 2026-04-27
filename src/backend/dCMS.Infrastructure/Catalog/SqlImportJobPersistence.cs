using System.Text.Json;
using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace dCMS.Infrastructure.Catalog;

// DAI-684 / DAI-706: Dapper + Npgsql implementation of <see cref="IImportJobPersistence"/>.
public sealed class SqlImportJobPersistence(string connectionString) : IImportJobPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private sealed class Row
    {
        public string Id { get; init; } = null!;
        public string TenantId { get; init; } = null!;
        public string Type { get; init; } = null!;
        public string Status { get; init; } = null!;
        public string FileKey { get; init; } = null!;
        public int? Total { get; init; }
        public int Processed { get; init; }
        public string Errors { get; init; } = "[]";
        public string? LastProcessedKey { get; init; }
        public string CreatedBy { get; init; } = "system";
        public DateTime CreatedAt { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }

        public ImportJob ToModel()
        {
            var errs = string.IsNullOrWhiteSpace(Errors)
                ? Array.Empty<ImportRowError>()
                : JsonSerializer.Deserialize<ImportRowError[]>(Errors) ?? Array.Empty<ImportRowError>();
            return new ImportJob(
                Id, TenantId, Type, Status, FileKey, Total, Processed, errs, LastProcessedKey, CreatedBy,
                new DateTimeOffset(CreatedAt, TimeSpan.Zero),
                StartedAt.HasValue ? new DateTimeOffset(StartedAt.Value, TimeSpan.Zero) : null,
                CompletedAt.HasValue ? new DateTimeOffset(CompletedAt.Value, TimeSpan.Zero) : null);
        }
    }

    private const string AllCols = """
        "Id","TenantId","Type","Status","FileKey","Total","Processed","Errors","LastProcessedKey",
        "CreatedBy","CreatedAt","StartedAt","CompletedAt"
        """;

    public async Task CreateAsync(ImportJob job, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO "ImportJobs"
            ("Id","TenantId","Type","Status","FileKey","Total","Processed","Errors","LastProcessedKey",
             "CreatedBy","CreatedAt","StartedAt","CompletedAt")
            VALUES (@Id,@TenantId,@Type,@Status,@FileKey,@Total,@Processed,@Errors::jsonb,@LastProcessedKey,
                    @CreatedBy,@CreatedAt,@StartedAt,@CompletedAt)
            """, conn);
        cmd.Parameters.AddWithValue("Id", job.Id);
        cmd.Parameters.AddWithValue("TenantId", job.TenantId);
        cmd.Parameters.AddWithValue("Type", job.Type);
        cmd.Parameters.AddWithValue("Status", job.Status);
        cmd.Parameters.AddWithValue("FileKey", job.FileKey);
        cmd.Parameters.AddWithValue("Total", (object?)job.Total ?? DBNull.Value);
        cmd.Parameters.AddWithValue("Processed", job.Processed);
        cmd.Parameters.AddWithValue("Errors", JsonSerializer.Serialize(job.Errors));
        cmd.Parameters.AddWithValue("LastProcessedKey", (object?)job.LastProcessedKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("CreatedBy", job.CreatedBy);
        cmd.Parameters.AddWithValue("CreatedAt", job.CreatedAt.UtcDateTime);
        cmd.Parameters.AddWithValue("StartedAt", (object?)job.StartedAt?.UtcDateTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("CompletedAt", (object?)job.CompletedAt?.UtcDateTime ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<ImportJob?> GetAsync(string jobId, string tenantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"""SELECT {AllCols} FROM "ImportJobs" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
            new { Id = jobId, TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<ImportJob>> ListRecentAsync(string tenantId, int take, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            $"""SELECT {AllCols} FROM "ImportJobs" WHERE "TenantId"=@TenantId ORDER BY "CreatedAt" DESC LIMIT @Take""",
            new { TenantId = tenantId, Take = Math.Clamp(take, 1, 200) }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task MarkRunningAsync(string jobId, string tenantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "ImportJobs"
               SET "Status"='Running', "StartedAt"=COALESCE("StartedAt", NOW())
             WHERE "Id"=@Id AND "TenantId"=@TenantId AND "Status" IN ('Pending','Running')
            """, new { Id = jobId, TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpdateProgressAsync(string jobId, string tenantId, int processed, string lastProcessedKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "ImportJobs"
               SET "Processed"=@Processed, "LastProcessedKey"=@LastProcessedKey
             WHERE "Id"=@Id AND "TenantId"=@TenantId
            """, new { Id = jobId, TenantId = tenantId, Processed = processed, LastProcessedKey = lastProcessedKey },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task AppendErrorAsync(string jobId, string tenantId, ImportRowError error, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            UPDATE "ImportJobs"
               SET "Errors" = COALESCE("Errors",'[]'::jsonb) || @Err::jsonb
             WHERE "Id"=@Id AND "TenantId"=@TenantId
            """, conn);
        cmd.Parameters.AddWithValue("Id", jobId);
        cmd.Parameters.AddWithValue("TenantId", tenantId);
        var errParam = cmd.Parameters.Add("Err", NpgsqlDbType.Text);
        errParam.Value = JsonSerializer.Serialize(new[] { error });
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(string jobId, string tenantId, string finalStatus, int processed,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "ImportJobs"
               SET "Status"=@Status, "Processed"=@Processed, "CompletedAt"=NOW()
             WHERE "Id"=@Id AND "TenantId"=@TenantId
            """, new { Id = jobId, TenantId = tenantId, Status = finalStatus, Processed = processed },
            cancellationToken: ct)).ConfigureAwait(false);
    }
}
