using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

public sealed class PgOrderFailureRepository : IOrderFailureRepository
{
    private readonly string _connectionString;

    public PgOrderFailureRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<IReadOnlyList<OrderFailureRow>> ListAsync(
        string tenantId,
        string storeId,
        string? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var take = limit + 1;

        if (!TryDecodeCursor(cursor, out var cursorFailedAt, out var cursorOrderId))
            throw new ArgumentException("Invalid cursor.", nameof(cursor));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT
                "OrderId",
                "TenantId",
                "StoreId",
                "FailureStatus",
                "FailureReason",
                "FailureErrorCode",
                "SourceEventId",
                "FailedAt",
                "RetryCount",
                "LastRetryAt",
                "ResolvedAt",
                "ResolvedBy",
                "LogJson"::text AS LogJson
            FROM "OrderFailures"
            WHERE "TenantId" = @TenantId
              AND "StoreId" = @StoreId
              AND "ResolvedAt" IS NULL
              AND (@Status IS NULL OR "FailureStatus" = @Status)
              AND (
                  @HasCursor = FALSE
                  OR ("FailedAt", "OrderId") < (@CursorFailedAt::timestamptz, @CursorOrderId::uuid)
              )
            ORDER BY "FailedAt" DESC, "OrderId" DESC
            LIMIT @Take
            """;

        var rows = (await conn.QueryAsync<OrderFailureRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    StoreId = storeId,
                    Status = string.IsNullOrWhiteSpace(status) ? (string?)null : status.Trim(),
                    HasCursor = cursorFailedAt.HasValue && cursorOrderId.HasValue,
                    CursorFailedAt = cursorFailedAt,
                    CursorOrderId = cursorOrderId,
                    Take = take,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        return rows;
    }

    public async Task<OrderFailureRow?> GetAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT
                "OrderId",
                "TenantId",
                "StoreId",
                "FailureStatus",
                "FailureReason",
                "FailureErrorCode",
                "SourceEventId",
                "FailedAt",
                "RetryCount",
                "LastRetryAt",
                "ResolvedAt",
                "ResolvedBy",
                "LogJson"::text AS LogJson
            FROM "OrderFailures"
            WHERE "OrderId" = @OrderId AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;

        return await conn.QuerySingleOrDefaultAsync<OrderFailureRow>(
            new CommandDefinition(sql, new { OrderId = orderId, TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpsertFailureAsync(
        OrderFailureRow row,
        string logEntryJson,
        CancellationToken cancellationToken = default)
    {
        logEntryJson = (logEntryJson ?? "").Trim();
        if (logEntryJson.Length == 0)
            logEntryJson = "{}";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            INSERT INTO "OrderFailures"
            (
                "OrderId",
                "TenantId",
                "StoreId",
                "FailureStatus",
                "FailureReason",
                "FailureErrorCode",
                "SourceEventId",
                "FailedAt",
                "RetryCount",
                "LastRetryAt",
                "ResolvedAt",
                "ResolvedBy",
                "LogJson"
            )
            VALUES
            (
                @OrderId,
                @TenantId,
                @StoreId,
                @FailureStatus,
                @FailureReason,
                @FailureErrorCode,
                @SourceEventId,
                @FailedAt,
                @RetryCount,
                @LastRetryAt,
                NULL,
                NULL,
                COALESCE(@LogEntryArr::jsonb, '[]'::jsonb)
            )
            ON CONFLICT ("OrderId") DO UPDATE
            SET
                "FailureStatus" = EXCLUDED."FailureStatus",
                "FailureReason" = EXCLUDED."FailureReason",
                "FailureErrorCode" = EXCLUDED."FailureErrorCode",
                "SourceEventId" = COALESCE(EXCLUDED."SourceEventId", "OrderFailures"."SourceEventId"),
                "FailedAt" = EXCLUDED."FailedAt",
                "ResolvedAt" = NULL,
                "ResolvedBy" = NULL,
                "LogJson" = "OrderFailures"."LogJson" || COALESCE(@LogEntryArr::jsonb, '[]'::jsonb)
            """;

        // Append as a JSON array item (LogJson is array)
        var logEntryArr = $"[{logEntryJson}]";
        await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    row.OrderId,
                    row.TenantId,
                    row.StoreId,
                    row.FailureStatus,
                    row.FailureReason,
                    row.FailureErrorCode,
                    row.SourceEventId,
                    row.FailedAt,
                    row.RetryCount,
                    row.LastRetryAt,
                    LogEntryArr = logEntryArr,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> MarkResolvedAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        string resolvedBy,
        CancellationToken cancellationToken = default)
    {
        resolvedBy = (resolvedBy ?? "").Trim();
        if (resolvedBy.Length == 0)
            resolvedBy = "system";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            UPDATE "OrderFailures"
            SET "ResolvedAt" = @Now,
                "ResolvedBy" = @ResolvedBy
            WHERE "OrderId" = @OrderId
              AND "TenantId" = @TenantId
              AND "StoreId" = @StoreId
              AND "ResolvedAt" IS NULL
            """;

        var n = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow, ResolvedBy = resolvedBy, OrderId = orderId, TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> IncrementRetryAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            UPDATE "OrderFailures"
            SET "RetryCount" = "RetryCount" + 1,
                "LastRetryAt" = @Now
            WHERE "OrderId" = @OrderId
              AND "TenantId" = @TenantId
              AND "StoreId" = @StoreId
              AND "ResolvedAt" IS NULL
            """;

        var n = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow, OrderId = orderId, TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset? failedAt, out Guid? orderId)
    {
        failedAt = null;
        orderId = null;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Trim()));
            var parts = raw.Split('|');
            if (parts.Length != 2) return false;
            failedAt = DateTimeOffset.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind);
            orderId = Guid.Parse(parts[1]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string EncodeCursor(DateTimeOffset failedAt, Guid orderId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{failedAt:O}|{orderId:D}"));
}

