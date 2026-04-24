using System.Text.Json;
using Dapper;
using dCMS.Order.Core.Ordering;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>Read-side access (no ambient transaction).</summary>
public sealed class OrderQueryStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _connectionString;

    public OrderQueryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<Core.Domain.Order?> GetByIdAsync(
        string tenantId,
        string storeId,
        string orderId,
        CancellationToken cancellationToken = default) =>
        (await GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false))?.Order;

    public async Task<TimedOrder?> GetTimedByIdAsync(
        string tenantId,
        string storeId,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            return null;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string orderSql = """
            SELECT "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "Total",
                   "ShippingAddress"::text AS ShippingAddressJson,
                   "PaymentIntentId",
                   "FailureReason",
                   "FailureErrorCode",
                   "FailedAt",
                   "RetryCount",
                   "CreatedAt"
            FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            new CommandDefinition(orderSql, new { Id = orderGuid, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
            return null;

        const string itemsSql = """
            SELECT "Id", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal",
                   "ProductName"::text AS ProductNameJson, "VariantSnapshot"::text AS VariantSnapshotJson
            FROM "OrderItems"
            WHERE "OrderId" = @OrderId
            ORDER BY "Id"
            """;

        var itemRows = await connection.QueryAsync<OrderItemRow>(
            new CommandDefinition(itemsSql, new { OrderId = orderGuid }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var order = MapOrder(row, itemRows);
        return new TimedOrder(order, ToUtcOffset(row.CreatedAt));
    }

    public async Task<Core.Domain.Order?> GetByIdempotencyKeyAsync(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string orderSql = """
            SELECT "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "Total",
                   "ShippingAddress"::text AS ShippingAddressJson,
                   "PaymentIntentId",
                   "FailureReason",
                   "FailureErrorCode",
                   "FailedAt",
                   "RetryCount",
                   "CreatedAt"
            FROM "Orders"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId AND "IdempotencyKey" = @IdempotencyKey
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            new CommandDefinition(orderSql, new { TenantId = tenantId, StoreId = storeId, IdempotencyKey = idempotencyKey },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
            return null;

        const string itemsSql = """
            SELECT "Id", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal",
                   "ProductName"::text AS ProductNameJson, "VariantSnapshot"::text AS VariantSnapshotJson
            FROM "OrderItems"
            WHERE "OrderId" = @OrderId
            ORDER BY "Id"
            """;

        var itemRows = await connection.QueryAsync<OrderItemRow>(
            new CommandDefinition(itemsSql, new { OrderId = row.Id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return MapOrder(row, itemRows);
    }

    public async Task<OrderListPage> ListOrdersAsync(OrderListQuery query, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var take = limit + 1;

        if (!OrderListCursorCodec.TryDecode(query.Cursor, out var cursorCreated, out var cursorId))
            throw new ArgumentException("Invalid cursor.", nameof(OrderListQuery.Cursor));

        string[]? dbStatuses = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var parts = query.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 10)
                throw new ArgumentException("At most 10 statuses are allowed.", nameof(query));

            var mapped = new List<string>(parts.Length);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in parts)
            {
                if (!seen.Add(p))
                    continue;
                if (!TryMapApiStatusToDb(p, out var db))
                    throw new ArgumentException($"Invalid status filter: {p}", nameof(query));
                mapped.Add(db);
            }

            dbStatuses = mapped.Count > 0 ? mapped.ToArray() : null;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string listSql = """
            SELECT "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "Total",
                   "ShippingAddress"::text AS ShippingAddressJson,
                   "PaymentIntentId",
                   "FailureReason",
                   "FailureErrorCode",
                   "FailedAt",
                   "RetryCount",
                   "CreatedAt"
            FROM "Orders"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND (@CustomerId IS NULL OR "CustomerId" = @CustomerId)
              AND (@DbStatuses::text[] IS NULL OR "Status" = ANY(@DbStatuses::text[]))
              AND (
                  @HasCursor = FALSE
                  OR ("CreatedAt", "Id") < (@CursorCreated::timestamptz, @CursorId::uuid)
              )
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Take
            """;

        var rows = (await connection.QueryAsync<OrderRow>(
            new CommandDefinition(
                listSql,
                new
                {
                    TenantId = query.TenantId,
                    StoreId = query.StoreId,
                    CustomerId = string.IsNullOrWhiteSpace(query.CustomerId) ? (string?)null : query.CustomerId.Trim(),
                    DbStatuses = dbStatuses,
                    HasCursor = cursorCreated.HasValue && cursorId.HasValue,
                    CursorCreated = cursorCreated,
                    CursorId = cursorId,
                    Take = take,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        if (rows.Count == 0)
            return new OrderListPage([], null);

        var orderGuids = rows.Select(r => r.Id).ToArray();
        const string itemsSql = """
            SELECT "Id", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal",
                   "ProductName"::text AS ProductNameJson, "VariantSnapshot"::text AS VariantSnapshotJson,
                   "OrderId"
            FROM "OrderItems"
            WHERE "OrderId" = ANY(@OrderIds)
            ORDER BY "OrderId", "Id"
            """;

        var itemRows = await connection.QueryAsync<OrderItemRowWithOrder>(
            new CommandDefinition(itemsSql, new { OrderIds = orderGuids }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var itemsByOrder = itemRows.GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        var timed = new List<TimedOrder>(rows.Count);
        foreach (var row in rows)
        {
            var items = itemsByOrder.GetValueOrDefault(row.Id) ?? [];
            var order = MapOrder(row, items.Select(x => new OrderItemRow(
                x.Id,
                x.VariantId,
                x.ProductId,
                x.Quantity,
                x.UnitPrice,
                x.LineTotal,
                x.ProductNameJson,
                x.VariantSnapshotJson)));
            timed.Add(new TimedOrder(order, ToUtcOffset(row.CreatedAt)));
        }

        string? next = null;
        if (hasMore)
        {
            var last = timed[^1];
            next = OrderListCursorCodec.Encode(last.CreatedAt, Guid.Parse(last.Order.Id));
        }

        return new OrderListPage(timed, next);
    }

    /// <summary>
    /// DAI-651 — Refund cases are cancelled orders that have a PaymentTransaction.
    /// This returns the raw cancelled orders page; callers should enrich with payment info and filter if needed.
    /// DAI-653 — optional <paramref name="refundStatusFilter"/> narrows by <c>Orders.RefundStatus</c>.
    /// </summary>
    public async Task<RefundCaseOrderPage> ListCancelledOrdersForRefundCasesAsync(
        string tenantId,
        string storeId,
        RefundStatusListFilter? refundStatusFilter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var lim = Math.Clamp(limit, 1, 100);
        var take = lim + 1;

        if (!OrderListCursorCodec.TryDecode(cursor, out var cursorCreated, out var cursorId))
            throw new ArgumentException("Invalid cursor.", nameof(cursor));

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT
                "Id"::uuid AS Id,
                "TenantId" AS TenantId,
                "StoreId" AS StoreId,
                "CustomerId" AS CustomerId,
                "PaymentIntentId" AS PaymentIntentId,
                "Total" AS Total,
                "Currency" AS Currency,
                "RefundStatus" AS RefundStatus,
                "RefundRemark" AS RefundRemark,
                "RefundedAt" AS RefundedAt,
                "UpdatedAt" AS UpdatedAt,
                "CreatedAt" AS CreatedAt
            FROM "Orders"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND "Status" IN ('Cancelled', 'AdminCancelled', 'UserCancelled')
              AND "PaymentIntentId" IS NOT NULL
              AND (
                  @FilterNone = TRUE
                  OR (
                      @FilterPending = TRUE AND (
                          "RefundStatus" IS NULL
                          OR TRIM(COALESCE("RefundStatus", '')) = ''
                          OR LOWER(TRIM("RefundStatus")) IN ('pending_refund', 'pending')
                      )
                  )
                  OR (@FilterExact IS NOT NULL AND "RefundStatus" = @FilterExact)
              )
              AND (
                  @HasCursor = FALSE
                  OR ("CreatedAt", "Id") < (@CursorCreated::timestamptz, @CursorId::uuid)
              )
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Take
            """;

        var rows = (await connection.QueryAsync<RefundCaseOrderRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                FilterNone = refundStatusFilter is null,
                FilterPending = refundStatusFilter?.MatchPending == true,
                FilterExact = refundStatusFilter?.ExactDbValue,
                HasCursor = cursorCreated.HasValue && cursorId.HasValue,
                CursorCreated = cursorCreated,
                CursorId = cursorId,
                Take = take,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        var hasMore = rows.Count > lim;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        string? next = null;
        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            next = OrderListCursorCodec.Encode(AsUtcOffset(last.CreatedAt), last.Id);
        }

        return new RefundCaseOrderPage(rows, next);
    }

    /// <summary>DAI-653 — single cancelled order row suitable for refund-case read, or <see langword="null"/>.</summary>
    public async Task<RefundCaseOrderRow?> TryGetRefundCaseOrderAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT
                "Id"::uuid AS Id,
                "TenantId" AS TenantId,
                "StoreId" AS StoreId,
                "CustomerId" AS CustomerId,
                "PaymentIntentId" AS PaymentIntentId,
                "Total" AS Total,
                "Currency" AS Currency,
                "RefundStatus" AS RefundStatus,
                "RefundRemark" AS RefundRemark,
                "RefundedAt" AS RefundedAt,
                "UpdatedAt" AS UpdatedAt,
                "CreatedAt" AS CreatedAt
            FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND "Status" IN ('Cancelled', 'AdminCancelled', 'UserCancelled')
              AND "PaymentIntentId" IS NOT NULL
            """;

        return await connection.QuerySingleOrDefaultAsync<RefundCaseOrderRow>(
            new CommandDefinition(sql, new { Id = orderId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static DateTimeOffset AsUtcOffset(DateTime utcOrUnspecified) =>
        utcOrUnspecified.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc))
            : new DateTimeOffset(utcOrUnspecified.ToUniversalTime());

    private static bool TryMapApiStatusToDb(string api, out string db)
    {
        db = api.ToLowerInvariant() switch
        {
            "payment_pending" => nameof(Core.Domain.OrderStatus.PaymentPending),
            "confirmed" => nameof(Core.Domain.OrderStatus.Confirmed),
            "processing" => nameof(Core.Domain.OrderStatus.Processing),
            "shipped" => nameof(Core.Domain.OrderStatus.Shipped),
            "delivered" => nameof(Core.Domain.OrderStatus.Delivered),
            "cancelled" => nameof(Core.Domain.OrderStatus.Cancelled),
            // DAI-637 failure states
            "payment_failed" => nameof(Core.Domain.OrderStatus.PaymentFailed),
            "auth_failed" => nameof(Core.Domain.OrderStatus.AuthFailed),
            "address_error" => nameof(Core.Domain.OrderStatus.AddressError),
            "stock_error" => nameof(Core.Domain.OrderStatus.StockError),
            "system_error" => nameof(Core.Domain.OrderStatus.SystemError),
            _ => "",
        };
        return db.Length > 0;
    }

    private static DateTimeOffset ToUtcOffset(DateTime createdAt) =>
        new(DateTime.SpecifyKind(createdAt, DateTimeKind.Utc));

    private static Core.Domain.Order MapOrder(OrderRow row, IEnumerable<OrderItemRow> itemRows)
    {
        var status = Enum.Parse<Core.Domain.OrderStatus>(row.Status, ignoreCase: true);
        var ship = JsonSerializer.Deserialize<Core.Domain.ShippingAddress>(row.ShippingAddressJson, Json)
                   ?? new Core.Domain.ShippingAddress("", null, "", "", "", "");

        var items = itemRows.Select(ir =>
        {
            var productName = JsonSerializer.Deserialize<string>(ir.ProductNameJson, Json)
                              ?? ir.ProductNameJson.Trim('"');
            var variantJson = string.IsNullOrWhiteSpace(ir.VariantSnapshotJson) ? "{}" : ir.VariantSnapshotJson;
            return new Core.Domain.OrderItem(
                ir.Id,
                ir.ProductId,
                ir.VariantId,
                ir.Quantity,
                new Core.Domain.Money(ir.UnitPrice, row.Currency),
                productName,
                variantJson);
        }).ToList();

        return Core.Domain.Order.FromPersistence(
            row.Id.ToString(),
            row.TenantId,
            row.StoreId,
            row.CustomerId,
            status,
            new Core.Domain.Money(row.Total, row.Currency),
            ship,
            items,
            row.PaymentIntentId,
            row.FailureReason,
            row.FailureErrorCode,
            row.FailedAt.HasValue ? ToUtcOffset(row.FailedAt.Value) : null,
            row.RetryCount);
    }

    private sealed record OrderRow(
        Guid Id,
        string TenantId,
        string StoreId,
        string CustomerId,
        string Status,
        string Currency,
        decimal Total,
        string ShippingAddressJson,
        string? PaymentIntentId,
        string? FailureReason,
        string? FailureErrorCode,
        DateTime? FailedAt,
        int RetryCount,
        DateTime CreatedAt);

    private sealed record OrderItemRow(
        string Id,
        string VariantId,
        string ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string ProductNameJson,
        string VariantSnapshotJson);

    private sealed record OrderItemRowWithOrder(
        string Id,
        string VariantId,
        string ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string ProductNameJson,
        string VariantSnapshotJson,
        Guid OrderId);

    public sealed record RefundCaseOrderPage(IReadOnlyList<RefundCaseOrderRow> Items, string? NextCursor);

    /// <summary>Row for refund-case list query — <see cref="DateTime"/> fields match Dapper/Npgsql reader types.</summary>
    public sealed class RefundCaseOrderRow
    {
        public Guid Id { get; set; }
        public string TenantId { get; set; } = "";
        public string StoreId { get; set; } = "";
        public string CustomerId { get; set; } = "";
        public string? PaymentIntentId { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; } = "";
        public string? RefundStatus { get; set; }
        public string RefundRemark { get; set; } = "";
        public DateTime? RefundedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
