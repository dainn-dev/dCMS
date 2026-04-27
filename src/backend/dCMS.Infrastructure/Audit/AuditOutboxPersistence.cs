using System.Text.Json;
using Dapper;
using dCMS.Core.Audit;
using dCMS.Core.Messaging;
using Npgsql;

namespace dCMS.Infrastructure.Audit;

/// <summary>
/// Per-service audit outbox: durable buffer for AuditLogQueuedV1 publishes (at-least-once).
/// Each service that audits owns an AuditOutbox table in its own DB.
/// </summary>
public sealed class AuditOutboxPersistence(string connectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task InsertAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        var contract = new AuditLogQueuedV1(
            entry.TenantId, entry.StoreId, entry.UserId, entry.UserRole, entry.Action,
            entry.EntityType, entry.EntityId, entry.Diff, entry.IpAddress, entry.CreatedAt);
        var payload = JsonSerializer.Serialize(contract, JsonOptions);

        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "AuditOutbox" ("Payload", "CreatedAt")
            VALUES (@Payload, @Now)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { Payload = payload, Now = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task ProcessPendingAsync(Func<AuditLogQueuedV1, Task> publishAsync, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string selectSql = """
            SELECT "Id", "Payload", "RetryCount"
            FROM "AuditOutbox"
            WHERE "ProcessedAt" IS NULL
            ORDER BY "Id"
            LIMIT 200
            """;
        var rows = (await conn.QueryAsync<AuditOutboxRow>(
            new CommandDefinition(selectSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        foreach (var row in rows)
        {
            AuditLogQueuedV1? msg;
            try
            {
                msg = JsonSerializer.Deserialize<AuditLogQueuedV1>(row.Payload, JsonOptions);
            }
            catch
            {
                msg = null;
            }
            if (msg is null)
            {
                await MarkProcessedAsync(row.Id, "skipped_invalid_payload", cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await publishAsync(msg).ConfigureAwait(false);
                await MarkProcessedAsync(row.Id, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await BumpRetryAsync(row.Id, ex.Message, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task MarkProcessedAsync(long id, string? note, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "AuditOutbox"
            SET "ProcessedAt" = @Now, "Error" = @Note
            WHERE "Id" = @Id
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, Note = note, Now = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task BumpRetryAsync(long id, string error, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "AuditOutbox"
            SET "RetryCount" = "RetryCount" + 1, "Error" = @Err
            WHERE "Id" = @Id
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, Err = Truncate(error, 2000) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed class AuditOutboxRow
    {
        public long Id { get; init; }
        public string Payload { get; init; } = null!;
        public int RetryCount { get; init; }
    }
}
