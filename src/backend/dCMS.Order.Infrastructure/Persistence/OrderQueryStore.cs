using System.Text;
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

        if (!TryDecodeCursor(query.Cursor, out var cursorCreated, out var cursorId))
            throw new ArgumentException("Invalid cursor.", nameof(OrderListQuery.Cursor));

        string? dbStatus = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!TryMapApiStatusToDb(query.Status.Trim(), out dbStatus))
                throw new ArgumentException("Invalid status filter.", nameof(query));
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string listSql = """
            SELECT "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "Total",
                   "ShippingAddress"::text AS ShippingAddressJson,
                   "PaymentIntentId",
                   "CreatedAt"
            FROM "Orders"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND (@CustomerId IS NULL OR "CustomerId" = @CustomerId)
              AND (@DbStatus IS NULL OR "Status" = @DbStatus)
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
                    DbStatus = dbStatus,
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
            next = EncodeCursor(last.CreatedAt, Guid.Parse(last.Order.Id));
        }

        return new OrderListPage(timed, next);
    }

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
            _ => "",
        };
        return db.Length > 0;
    }

    private static string EncodeCursor(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt:O}|{id:D}";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset? createdAt, out Guid? id)
    {
        createdAt = null;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var bytes = Convert.FromBase64String(padded);
            var s = Encoding.UTF8.GetString(bytes);
            var pipe = s.IndexOf('|', StringComparison.Ordinal);
            if (pipe <= 0 || pipe >= s.Length - 1)
                return false;
            if (!DateTimeOffset.TryParse(s.AsSpan(0, pipe), null, System.Globalization.DateTimeStyles.RoundtripKind, out var ca))
                return false;
            if (!Guid.TryParse(s.AsSpan(pipe + 1), out var gid))
                return false;
            createdAt = ca;
            id = gid;
            return true;
        }
        catch
        {
            return false;
        }
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
            row.PaymentIntentId);
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
}
