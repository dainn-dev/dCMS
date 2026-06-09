using System.Text.Json;
using Dapper;
using dCMS.Provisioning.Domain;
using Npgsql;

namespace dCMS.Infrastructure.Platform;

public sealed class SqlTenantWebhookSubscriptionRepository(string connectionString) : ITenantWebhookSubscriptionRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString = connectionString;

    public async Task<TenantWebhookSubscriptionRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT "Id", "TenantId", "Url", "Secret", "Events"::text AS EventsJson, "Status", "FailureCount", "CreatedAt", "UpdatedAt"
            FROM "TenantWebhookSubscriptions"
            WHERE "Id" = @Id
            """,
            new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<TenantWebhookSubscriptionRecord>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT "Id", "TenantId", "Url", "Secret", "Events"::text AS EventsJson, "Status", "FailureCount", "CreatedAt", "UpdatedAt"
            FROM "TenantWebhookSubscriptions"
            WHERE "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TenantWebhookSubscriptionRecord>> ListActiveByTenantAndEventAsync(
        string tenantId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT "Id", "TenantId", "Url", "Secret", "Events"::text AS EventsJson, "Status", "FailureCount", "CreatedAt", "UpdatedAt"
            FROM "TenantWebhookSubscriptions"
            WHERE "TenantId" = @TenantId AND "Status" = 'active' AND "Events" ? @EventType
            """,
            new { TenantId = tenantId, EventType = eventType }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task CreateAsync(TenantWebhookSubscriptionRecord record, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "TenantWebhookSubscriptions" (
                "Id", "TenantId", "Url", "Secret", "Events", "Status", "FailureCount", "CreatedAt", "UpdatedAt")
            VALUES (@Id, @TenantId, @Url, @Secret, @Events::jsonb, @Status, @FailureCount, @CreatedAt, @UpdatedAt)
            """, new
        {
            record.Id,
            record.TenantId,
            record.Url,
            record.Secret,
            Events = JsonSerializer.Serialize(record.Events, Json),
            Status = record.Status.ToDbString(),
            record.FailureCount,
            CreatedAt = record.CreatedAt.UtcDateTime,
            UpdatedAt = record.UpdatedAt.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateStatusAsync(
        string id,
        WebhookSubscriptionStatus status,
        int failureCount,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "TenantWebhookSubscriptions"
            SET "Status" = @Status, "FailureCount" = @FailureCount, "UpdatedAt" = NOW()
            WHERE "Id" = @Id
            """, new { Id = id, Status = status.ToDbString(), FailureCount = failureCount },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static TenantWebhookSubscriptionRecord Map(Row r) => new(
        r.Id,
        r.TenantId,
        r.Url,
        r.Secret,
        JsonSerializer.Deserialize<List<string>>(r.EventsJson ?? "[]", Json) ?? [],
        WebhookSubscriptionStatusExtensions.FromDbString(r.Status),
        r.FailureCount,
        new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
        new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero));

    private sealed class Row
    {
        public string Id { get; init; } = "";
        public string TenantId { get; init; } = "";
        public string Url { get; init; } = "";
        public string Secret { get; init; } = "";
        public string? EventsJson { get; init; }
        public string Status { get; init; } = "";
        public int FailureCount { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
