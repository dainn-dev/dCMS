using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Notification.Api.Routes;

public sealed class NotificationEventsRepository
{
    private readonly string _cs;

    public NotificationEventsRepository(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("Notification")
            ?? throw new InvalidOperationException("ConnectionStrings:Notification is required.");
    }

    public async Task<int> CountUnreadAsync(string tenantId, string userId, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*)::int FROM "NotificationEvents"
            WHERE "TenantId" = @TenantId AND "UserId" = @UserId AND "ReadAt" IS NULL
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationEventRow>> ListAsync(string tenantId, string userId, int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT "Id","TenantId","UserId","Type","EntityId","Message","ReadAt","CreatedAt"
            FROM "NotificationEvents"
            WHERE "TenantId" = @TenantId AND "UserId" = @UserId
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Limit
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<NotificationEventRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, Limit = Math.Clamp(limit, 1, 100) },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<int> MarkAllReadAsync(string tenantId, string userId, DateTimeOffset readAt, CancellationToken ct)
    {
        const string sql = """
            UPDATE "NotificationEvents"
            SET "ReadAt" = @ReadAt
            WHERE "TenantId" = @TenantId AND "UserId" = @UserId AND "ReadAt" IS NULL
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ReadAt = readAt },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task InsertAsync(string tenantId, string userId, string type, string entityId, string message,
        DateTimeOffset createdAt, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "NotificationEvents" ("TenantId","UserId","Type","EntityId","Message","ReadAt","CreatedAt")
            VALUES (@TenantId,@UserId,@Type,@EntityId,@Message,NULL,@CreatedAt)
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId, UserId = userId, Type = type, EntityId = entityId,
                Message = message, CreatedAt = createdAt,
            }, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}

public sealed class NotificationEventRow
{
    public long Id { get; init; }
    public string TenantId { get; init; } = "";
    public string UserId { get; init; } = "";
    public string Type { get; init; } = "";
    public string EntityId { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTimeOffset? ReadAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
