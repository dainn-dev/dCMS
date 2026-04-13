using Dapper;
using Npgsql;

namespace dCMS.Infrastructure.Outbox;

/// <summary>
/// Polls <c>OutboxEvents</c>, publishes to the bus delegate, marks processed or retries / dead-letters (≥5 failures).
/// </summary>
public sealed class SqlOutboxRelay(string connectionString)
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public Task ProcessPendingAsync(Func<object, Task> publishAsync, CancellationToken cancellationToken = default) =>
        ProcessPendingAsync((_, m) => publishAsync(m), cancellationToken);

    /// <summary>
    /// <paramref name="publishAsync"/> receives the outbox row id so transports can set a stable MassTransit message id
    /// (at-least-once relay + idempotent consumers — US-F5 / DAI-365).
    /// </summary>
    public async Task ProcessPendingAsync(
        Func<long, object, Task> publishAsync,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string selectSql = """
            SELECT o."Id", o."EventType", o."Payload", o."RetryCount"
            FROM "OutboxEvents" AS o
            WHERE o."ProcessedAt" IS NULL
            ORDER BY o."Id"
            LIMIT 100
            """;
        var rows = (await conn.QueryAsync<OutboxRowDto>(
            new CommandDefinition(selectSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        foreach (var row in rows)
        {
            var message = OutboxMessageDeserializer.Deserialize(row.EventType, row.Payload);
            if (message is null)
            {
                await MarkProcessedAsync(row.Id, "skipped_unknown_type", cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await publishAsync(row.Id, message).ConfigureAwait(false);
                await MarkProcessedAsync(row.Id, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await RecordFailureOrDeadLetterAsync(row, ex.Message, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task MarkProcessedAsync(long id, string? errorNote, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var sql = errorNote is null
            ? """
              UPDATE "OutboxEvents"
              SET "ProcessedAt" = @Now, "Error" = NULL
              WHERE "Id" = @Id
              """
            : """
              UPDATE "OutboxEvents"
              SET "ProcessedAt" = @Now, "Error" = @Note
              WHERE "Id" = @Id
              """;
        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Note = errorNote, Now = now },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task RecordFailureOrDeadLetterAsync(OutboxRowDto row, string failureMessage, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string bump = """
            UPDATE "OutboxEvents"
            SET "RetryCount" = "RetryCount" + 1,
                "Error" = @Err
            WHERE "Id" = @Id
            """;
        await conn.ExecuteAsync(new CommandDefinition(bump,
            new { Id = row.Id, Err = Truncate(failureMessage, 4000) }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var retryCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("""SELECT "RetryCount"::int FROM "OutboxEvents" WHERE "Id" = @Id""", new { row.Id }, tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (retryCount >= 5)
        {
            const string dl = """
                INSERT INTO "DeadLetterEvents" ("SourceOutboxId", "EventType", "Payload", "FailureReason", "FailedAt")
                VALUES (@SourceId, @EventType, @Payload, @Reason, @FailedAt)
                """;
            var failedAt = DateTimeOffset.UtcNow;
            await conn.ExecuteAsync(new CommandDefinition(dl,
                new
                {
                    SourceId = row.Id,
                    row.EventType,
                    row.Payload,
                    Reason = Truncate(failureMessage, 4000),
                    FailedAt = failedAt
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await conn.ExecuteAsync(new CommandDefinition(
                    """UPDATE "OutboxEvents" SET "ProcessedAt" = @Now WHERE "Id" = @Id""",
                    new { row.Id, Now = DateTimeOffset.UtcNow }, tx,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed class OutboxRowDto
    {
        public long Id { get; init; }
        public string EventType { get; init; } = null!;
        public string Payload { get; init; } = null!;
        public int RetryCount { get; init; }
    }
}
