using Dapper;
using dCMS.Core.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

public sealed class OrderReportQueryStore
{
    private readonly string _connectionString;

    public OrderReportQueryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<IReadOnlyList<TransactionSummaryRow>> GetTransactionSummaryAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        CancellationToken ct = default)
    {
        // DAI-689: Group by PaymentComponents.Type (Voucher / LoyaltyPoints / GiftCard / Gateway)
        // — one row per tender. Multi-tender orders contribute their per-component amount to each
        // bucket. Orders without a multi-tender plan are bucketed under 'Unknown'.
        const string sql = """
            SELECT
                COALESCE(pc."Type", 'Unknown') AS PaymentMethod,
                COUNT(DISTINCT o."Id")::int AS TransactionCount,
                SUM(COALESCE(pc."Amount", o."Total")) AS TotalAmount,
                o."Currency" AS Currency
            FROM "Orders" o
            LEFT JOIN "OrderPayments" op ON op."OrderId" = o."Id"
            LEFT JOIN "PaymentComponents" pc ON pc."OrderPaymentId" = op."Id"
                AND pc."State" IN ('Captured', 'Authorized', 'Refunded')
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed')
            GROUP BY COALESCE(pc."Type", 'Unknown'), o."Currency"
            ORDER BY SUM(COALESCE(pc."Amount", o."Total")) DESC
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<TransactionSummaryRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<TransactionDetailPage> GetTransactionDetailsAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        string? memberQuery, string? brandCode, string? paymentMethod,
        string? cursor, int limit,
        CancellationToken ct = default)
    {
        var lim = Math.Clamp(limit, 1, 100);
        var take = lim + 1;

        Guid cursorId = default;
        var hasCursor = !string.IsNullOrWhiteSpace(cursor) && Guid.TryParse(cursor, out cursorId);

        const string sql = """
            SELECT
                o."Id" AS OrderId,
                o."CreatedAt" AS Date,
                o."CustomerId" AS Member,
                o."StoreId" AS Store,
                o."Status" AS Status,
                o."Total" AS Amount,
                o."Currency" AS Currency
            FROM "Orders" o
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed')
              AND (@MemberQuery IS NULL OR o."CustomerId" ILIKE '%' || @MemberQuery || '%')
              AND (@HasCursor = FALSE OR o."Id" < @CursorId::uuid)
            ORDER BY o."CreatedAt" DESC, o."Id" DESC
            LIMIT @Take
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = (await conn.QueryAsync<TransactionDetailRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                MemberQuery = string.IsNullOrWhiteSpace(memberQuery) ? (string?)null : memberQuery.Trim(),
                HasCursor = hasCursor,
                CursorId = hasCursor ? cursorId : (Guid?)null,
                Take = take,
            }, cancellationToken: ct)).ConfigureAwait(false)).ToList();

        var hasMore = rows.Count > lim;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? rows[^1].OrderId.ToString() : null;
        return new TransactionDetailPage(rows, next);
    }

    public async Task<IReadOnlyList<EcommercePaymentRow>> GetEcommercePaymentsAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        string? paymentMethod,
        CancellationToken ct = default)
    {
        // DAI-689: Each multi-tender component is one row (matches the spec note about split tender).
        const string sql = """
            SELECT
                o."Id" AS OrderId,
                pc."Type" AS PaymentMethod,
                pc."Amount" AS Amount,
                o."Currency" AS Currency,
                COALESCE(pc."ExternalRef", pc."Reference", '') AS TransactionRef,
                pc."CreatedAt" AS Date
            FROM "Orders" o
            INNER JOIN "OrderPayments" op ON op."OrderId" = o."Id"
            INNER JOIN "PaymentComponents" pc ON pc."OrderPaymentId" = op."Id"
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND pc."State" IN ('Captured', 'Authorized', 'Refunded')
              AND (@PaymentMethod IS NULL OR pc."Type" = @PaymentMethod)
            ORDER BY pc."CreatedAt" DESC
            LIMIT 500
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<EcommercePaymentRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? (string?)null : paymentMethod.Trim(),
            }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SalesByCategoryRow>> GetSalesByCategoryAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        string? brandCode,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COALESCE(oi."ProductName"::text, '"Unknown"') AS Category,
                COUNT(DISTINCT oi."ProductId") AS ProductsCount,
                COUNT(DISTINCT o."Id") AS Transactions,
                SUM(oi."Quantity") AS UnitsSold,
                SUM(oi."LineTotal") AS TotalSales,
                o."Currency" AS Currency
            FROM "Orders" o
            INNER JOIN "OrderItems" oi ON oi."OrderId" = o."Id"
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed', 'Cancelled', 'AdminCancelled', 'UserCancelled')
            GROUP BY COALESCE(oi."ProductName"::text, '"Unknown"'), o."Currency"
            ORDER BY SUM(oi."LineTotal") DESC
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SalesByCategoryRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SalesByProductRow>> GetSalesByProductAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        string? brandCode, string? categoryId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                oi."ProductId" AS ProductId,
                oi."ProductName"::text AS ProductNameJson,
                SUM(oi."Quantity") AS UnitsSold,
                SUM(oi."LineTotal") AS TotalSales,
                o."Currency" AS Currency
            FROM "Orders" o
            INNER JOIN "OrderItems" oi ON oi."OrderId" = o."Id"
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed', 'Cancelled', 'AdminCancelled', 'UserCancelled')
            GROUP BY oi."ProductId", oi."ProductName"::text, o."Currency"
            ORDER BY SUM(oi."LineTotal") DESC
            LIMIT 500
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SalesByProductRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    [CrossTenantAllowed("SuperAdmin sales-by-tenant aggregation aggregates across all tenants by design")]
    public async Task<IReadOnlyList<SalesByTenantRow>> GetSalesByTenantAsync(
        DateOnly dateFrom, DateOnly dateTo,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o."TenantId" AS TenantId,
                COUNT(DISTINCT o."Id") AS OrdersCount,
                SUM(oi."Quantity") AS ProductsSold,
                SUM(o."Total") AS TotalSales,
                o."Currency" AS Currency
            FROM "Orders" o
            LEFT JOIN "OrderItems" oi ON oi."OrderId" = o."Id"
            WHERE o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed', 'Cancelled', 'AdminCancelled', 'UserCancelled')
            GROUP BY o."TenantId", o."Currency"
            ORDER BY SUM(o."Total") DESC
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SalesByTenantRow>(
            new CommandDefinition(sql, new
            {
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }
}

public sealed record TransactionSummaryRow(string PaymentMethod, int TransactionCount, decimal TotalAmount, string Currency);
public sealed record TransactionDetailRow(Guid OrderId, DateTime Date, string Member, string Store, string Status, decimal Amount, string Currency);
public sealed record TransactionDetailPage(IReadOnlyList<TransactionDetailRow> Items, string? NextCursor);
public sealed record EcommercePaymentRow(Guid OrderId, string PaymentMethod, decimal Amount, string Currency, string TransactionRef, DateTime Date);
public sealed record SalesByCategoryRow(string Category, int ProductsCount, int Transactions, int UnitsSold, decimal TotalSales, string Currency);
public sealed record SalesByProductRow(string ProductId, string ProductNameJson, int UnitsSold, decimal TotalSales, string Currency);
public sealed record SalesByTenantRow(string TenantId, int OrdersCount, int ProductsSold, decimal TotalSales, string Currency);
