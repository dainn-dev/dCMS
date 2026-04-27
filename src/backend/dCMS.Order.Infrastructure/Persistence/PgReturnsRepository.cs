using Dapper;
using dCMS.Order.Core.Domain;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>DAI-697 — read-side store for Returns + ReturnItems (no ambient transaction).</summary>
public sealed class PgReturnsRepository : IReturnsRepository
{
    private readonly string _connectionString;

    public PgReturnsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<Return?> GetByIdAsync(
        string tenantId,
        string storeId,
        Guid returnId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string headerSql = """
            SELECT "Id", "OrderId", "TenantId", "StoreId", "Status", "Reason", "Notes",
                   "RefundCaseId", "CreatedAt", "ApprovedAt", "ApprovedBy", "CompletedAt"
            FROM "Returns"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;

        var header = await conn.QuerySingleOrDefaultAsync<ReturnRow>(new CommandDefinition(
            headerSql, new { Id = returnId, TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (header is null)
            return null;

        const string itemsSql = """
            SELECT "Id", "OrderItemId", "Quantity", "Reason"
            FROM "ReturnItems"
            WHERE "ReturnId" = @ReturnId
            ORDER BY "Id"
            """;

        var rows = await conn.QueryAsync<ReturnItemRow>(new CommandDefinition(
            itemsSql, new { ReturnId = returnId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return MapReturn(header, rows);
    }

    public async Task<IReadOnlyList<Return>> ListByOrderAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string headerSql = """
            SELECT "Id", "OrderId", "TenantId", "StoreId", "Status", "Reason", "Notes",
                   "RefundCaseId", "CreatedAt", "ApprovedAt", "ApprovedBy", "CompletedAt"
            FROM "Returns"
            WHERE "OrderId" = @OrderId AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            ORDER BY "CreatedAt" DESC
            """;

        var headers = (await conn.QueryAsync<ReturnRow>(new CommandDefinition(
            headerSql, new { OrderId = orderId, TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (headers.Count == 0)
            return [];

        var ids = headers.Select(h => h.Id).ToArray();

        const string itemsSql = """
            SELECT "Id", "OrderItemId", "Quantity", "Reason", "ReturnId"
            FROM "ReturnItems"
            WHERE "ReturnId" = ANY(@Ids)
            ORDER BY "ReturnId", "Id"
            """;

        var itemRows = await conn.QueryAsync<ReturnItemRowWithParent>(new CommandDefinition(
            itemsSql, new { Ids = ids }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var grouped = itemRows.GroupBy(x => x.ReturnId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        return headers
            .Select(h => MapReturn(
                h,
                grouped.GetValueOrDefault(h.Id, Array.Empty<ReturnItemRowWithParent>())
                    .Select(x => new ReturnItemRow(x.Id, x.OrderItemId, x.Quantity, x.Reason))))
            .ToList();
    }

    public async Task<IReadOnlyList<Return>> ListByStatusAsync(
        string tenantId,
        string storeId,
        ReturnStatus? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var lim = Math.Clamp(limit, 1, 100);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string headerSql = """
            SELECT "Id", "OrderId", "TenantId", "StoreId", "Status", "Reason", "Notes",
                   "RefundCaseId", "CreatedAt", "ApprovedAt", "ApprovedBy", "CompletedAt"
            FROM "Returns"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND (@Status IS NULL OR "Status" = @Status)
            ORDER BY "CreatedAt" DESC
            LIMIT @Take
            """;

        var headers = (await conn.QueryAsync<ReturnRow>(new CommandDefinition(
            headerSql,
            new { TenantId = tenantId, StoreId = storeId, Status = status?.ToString(), Take = lim },
            cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (headers.Count == 0)
            return [];

        var ids = headers.Select(h => h.Id).ToArray();

        const string itemsSql = """
            SELECT "Id", "OrderItemId", "Quantity", "Reason", "ReturnId"
            FROM "ReturnItems"
            WHERE "ReturnId" = ANY(@Ids)
            ORDER BY "ReturnId", "Id"
            """;

        var itemRows = await conn.QueryAsync<ReturnItemRowWithParent>(new CommandDefinition(
            itemsSql, new { Ids = ids }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var grouped = itemRows.GroupBy(x => x.ReturnId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        return headers
            .Select(h => MapReturn(
                h,
                grouped.GetValueOrDefault(h.Id, Array.Empty<ReturnItemRowWithParent>())
                    .Select(x => new ReturnItemRow(x.Id, x.OrderItemId, x.Quantity, x.Reason))))
            .ToList();
    }

    private static Return MapReturn(ReturnRow header, IEnumerable<ReturnItemRow> items)
    {
        var status = Enum.Parse<ReturnStatus>(header.Status, ignoreCase: true);
        var reason = Enum.Parse<ReturnReason>(header.Reason, ignoreCase: true);
        var domainItems = items.Select(r => new ReturnItem(
            r.Id.ToString(),
            r.OrderItemId,
            r.Quantity,
            ParseReason(r.Reason))).ToList();

        return new Return(
            id: header.Id.ToString(),
            orderId: header.OrderId.ToString(),
            tenantId: header.TenantId,
            storeId: header.StoreId,
            reason: reason,
            notes: header.Notes,
            items: domainItems,
            status: status,
            refundCaseId: header.RefundCaseId?.ToString(),
            createdAt: ToOffset(header.CreatedAt),
            approvedAt: header.ApprovedAt is null ? null : ToOffset(header.ApprovedAt.Value),
            approvedBy: header.ApprovedBy,
            completedAt: header.CompletedAt is null ? null : ToOffset(header.CompletedAt.Value));
    }

    private static ReturnReason? ParseReason(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null
            : Enum.TryParse<ReturnReason>(raw, ignoreCase: true, out var r) ? r : null;

    private static DateTimeOffset ToOffset(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
            : new DateTimeOffset(dt.ToUniversalTime());

    private sealed record ReturnRow(
        Guid Id,
        Guid OrderId,
        string TenantId,
        string StoreId,
        string Status,
        string Reason,
        string? Notes,
        Guid? RefundCaseId,
        DateTime CreatedAt,
        DateTime? ApprovedAt,
        string? ApprovedBy,
        DateTime? CompletedAt);

    private sealed record ReturnItemRow(Guid Id, string OrderItemId, int Quantity, string? Reason);

    private sealed record ReturnItemRowWithParent(Guid Id, string OrderItemId, int Quantity, string? Reason, Guid ReturnId);
}
