using Dapper;
using dCMS.Provisioning.Domain;
using Npgsql;

namespace dCMS.Infrastructure.Platform;

public sealed class SqlTenantWebhookDeliveryRepository(string connectionString) : ITenantWebhookDeliveryRepository
{
    private readonly string _connectionString = connectionString;

    public async Task<long> EnqueueAsync(
        string subscriptionId,
        string tenantId,
        string eventType,
        string payloadJson,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var id = await conn.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
            INSERT INTO "TenantWebhookDeliveries" (
                "SubscriptionId", "TenantId", "EventType", "PayloadJson", "IdempotencyKey", "Status")
            VALUES (@SubscriptionId, @TenantId, @EventType, @PayloadJson::jsonb, @IdempotencyKey, 'pending')
            ON CONFLICT ("SubscriptionId", "IdempotencyKey") DO NOTHING
            RETURNING "Id"
            """, new { SubscriptionId = subscriptionId, TenantId = tenantId, EventType = eventType, PayloadJson = payloadJson, IdempotencyKey = idempotencyKey },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (id is not null)
            return id.Value;

        return await conn.QuerySingleAsync<long>(new CommandDefinition(
            """
            SELECT "Id" FROM "TenantWebhookDeliveries"
            WHERE "SubscriptionId" = @SubscriptionId AND "IdempotencyKey" = @IdempotencyKey
            """,
            new { SubscriptionId = subscriptionId, IdempotencyKey = idempotencyKey },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TenantWebhookDeliveryRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT "Id", "SubscriptionId", "TenantId", "EventType", "PayloadJson"::text AS PayloadJson, "IdempotencyKey",
                   "Status", "AttemptCount", "LastHttpStatus", "LastError", "CreatedAt", "DeliveredAt"
            FROM "TenantWebhookDeliveries"
            WHERE "Id" = @Id
            """,
            new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task UpdateDeliveryResultAsync(
        long id,
        WebhookDeliveryStatus status,
        int attemptCount,
        int? httpStatus,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "TenantWebhookDeliveries"
            SET "Status" = @Status,
                "AttemptCount" = @AttemptCount,
                "LastHttpStatus" = @HttpStatus,
                "LastError" = @Error,
                "DeliveredAt" = CASE WHEN @Status = 'delivered' THEN NOW() ELSE "DeliveredAt" END
            WHERE "Id" = @Id
            """, new
        {
            Id = id,
            Status = status.ToDbString(),
            AttemptCount = attemptCount,
            HttpStatus = httpStatus,
            Error = error,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TenantWebhookDeliveryRecord>> ListDeadLetterAsync(
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT "Id", "SubscriptionId", "TenantId", "EventType", "PayloadJson"::text AS PayloadJson, "IdempotencyKey",
                   "Status", "AttemptCount", "LastHttpStatus", "LastError", "CreatedAt", "DeliveredAt"
            FROM "TenantWebhookDeliveries"
            WHERE "TenantId" = @TenantId AND "Status" = 'dead_letter'
            ORDER BY "CreatedAt" DESC
            LIMIT @Limit
            """,
            new { TenantId = tenantId, Limit = Math.Max(1, limit) }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    private static TenantWebhookDeliveryRecord Map(Row r) => new(
        r.Id,
        r.SubscriptionId,
        r.TenantId,
        r.EventType,
        r.PayloadJson ?? "{}",
        r.IdempotencyKey,
        WebhookDeliveryStatusExtensions.FromDbString(r.Status),
        r.AttemptCount,
        r.LastHttpStatus,
        r.LastError,
        new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
        r.DeliveredAt is null ? null : new DateTimeOffset(r.DeliveredAt.Value, TimeSpan.Zero));

    private sealed class Row
    {
        public long Id { get; init; }
        public string SubscriptionId { get; init; } = "";
        public string TenantId { get; init; } = "";
        public string EventType { get; init; } = "";
        public string? PayloadJson { get; init; }
        public string IdempotencyKey { get; init; } = "";
        public string Status { get; init; } = "";
        public int AttemptCount { get; init; }
        public int? LastHttpStatus { get; init; }
        public string? LastError { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? DeliveredAt { get; init; }
    }
}
