using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

public sealed class OrderDlqAdminRepository : IOrderDlqAdminRepository
{
    private readonly string _connectionString;

    public OrderDlqAdminRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<IReadOnlyList<OrderDlqListItem>> ListAsync(
        string? eventType,
        DateTimeOffset? failedFrom,
        DateTimeOffset? failedTo,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = """
            SELECT
                d."Id",
                d."EventType",
                d."Payload",
                d."FailureReason",
                d."FailedAt",
                COALESCE(o."RetryCount", 0)::int AS "SourceRetryCount",
                d."ReprocessedAt",
                d."DiscardedAt"
            FROM "DeadLetterEvents" AS d
            LEFT JOIN "OutboxEvents" AS o ON o."Id" = d."SourceOutboxId"
            WHERE (@eventType IS NULL OR d."EventType" = @eventType)
              AND (@from IS NULL OR d."FailedAt" >= @from)
              AND (@to IS NULL OR d."FailedAt" <= @to)
            ORDER BY d."FailedAt" DESC
            LIMIT 500
            """;

        var rows = (await conn.QueryAsync<OrderDlqListRow>(
            new CommandDefinition(
                sql,
                new
                {
                    eventType,
                    from = failedFrom,
                    to = failedTo,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return rows
            .Select(r => new OrderDlqListItem(
                r.Id,
                TryParseOrderId(r.Payload),
                r.EventType,
                r.FailureReason,
                r.FailedAt,
                r.SourceRetryCount,
                r.ReprocessedAt,
                r.DiscardedAt))
            .ToList();
    }

    public async Task<OrderDlqRow?> GetAsync(long deadLetterId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            SELECT d."Id", d."SourceOutboxId", d."EventType", d."Payload", d."FailureReason", d."FailedAt",
                   d."ReprocessedAt", d."DiscardedAt"
            FROM "DeadLetterEvents" AS d
            WHERE d."Id" = @id
            """;
        var row = await conn.QuerySingleOrDefaultAsync<OrderDlqRow>(
            new CommandDefinition(sql, new { id = deadLetterId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row;
    }

    public async Task<bool> RetryAsync(long deadLetterId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var row = await conn.QuerySingleOrDefaultAsync<OrderDlqRow>(
            new CommandDefinition(
                """
                SELECT d."Id", d."SourceOutboxId", d."EventType", d."Payload", d."FailureReason", d."FailedAt",
                       d."ReprocessedAt", d."DiscardedAt"
                FROM "DeadLetterEvents" AS d
                WHERE d."Id" = @id
                FOR UPDATE
                """,
                new { id = deadLetterId },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null || row.ReprocessedAt is not null || row.DiscardedAt is not null)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        const string insertOutbox = """
            INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
            VALUES (@EventType, @Payload, @CreatedAt, 0)
            """;
        await conn.ExecuteAsync(
            new CommandDefinition(
                insertOutbox,
                new
                {
                    row.EventType,
                    row.Payload,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        await conn.ExecuteAsync(
            new CommandDefinition(
                """UPDATE "DeadLetterEvents" SET "ReprocessedAt" = @Now WHERE "Id" = @Id""",
                new { Id = deadLetterId, Now = DateTimeOffset.UtcNow },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DiscardAsync(long deadLetterId, string reason, CancellationToken cancellationToken = default)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length == 0)
            reason = "discarded_by_admin";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            UPDATE "DeadLetterEvents"
            SET "DiscardedAt" = @Now,
                "DiscardReason" = @Reason
            WHERE "Id" = @Id
              AND "DiscardedAt" IS NULL
            """;
        var n = await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { Id = deadLetterId, Now = DateTimeOffset.UtcNow, Reason = reason },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    private static string? TryParseOrderId(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("orderId", out var p))
                return p.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private sealed class OrderDlqListRow
    {
        public long Id { get; init; }
        public string EventType { get; init; } = null!;
        public string Payload { get; init; } = null!;
        public string FailureReason { get; init; } = null!;
        public DateTimeOffset FailedAt { get; init; }
        public int SourceRetryCount { get; init; }
        public DateTimeOffset? ReprocessedAt { get; init; }
        public DateTimeOffset? DiscardedAt { get; init; }
    }
}
