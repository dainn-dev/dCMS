using Dapper;
using dCMS.Core.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

public sealed class OrderReportQueryStore
{
    private readonly string _connectionString;
    private readonly PaymentTransactionQueryStore _paymentQueries;

    public OrderReportQueryStore(IConfiguration configuration, PaymentTransactionQueryStore paymentQueries)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _paymentQueries = paymentQueries;
    }

    public async Task<IReadOnlyList<TransactionSummaryRow>> GetTransactionSummaryAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        CancellationToken ct = default)
    {
        // DAI-XXX: Cross-DB JOIN to "PaymentTransactions" (lives in dcms_payment) was removed.
        // Now: 1) read order set from Order DB, 2) batch-fetch latest payment per order from Payment DB,
        // 3) aggregate in-process. Multi-tender (PaymentComponents) collapses to the latest top-level row;
        // gateway PaymentMethod is used as the canonical bucket for the "summary by method" view.
        const string ordersSql = """
            SELECT o."Id" AS OrderId, o."Total" AS Total, o."Currency" AS Currency
            FROM "Orders" o
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed')
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var orderRows = (await conn.QueryAsync<(Guid OrderId, decimal Total, string Currency)>(
            new CommandDefinition(ordersSql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false)).ToList();

        if (orderRows.Count == 0)
            return Array.Empty<TransactionSummaryRow>();

        var orderIds = orderRows.Select(o => o.OrderId).ToList();
        var paymentByOrder = await _paymentQueries.GetLatestByOrderIdsAsync(orderIds, ct).ConfigureAwait(false);

        return orderRows
            .GroupBy(o =>
            {
                var hasPayment = paymentByOrder.TryGetValue(o.OrderId, out var p);
                return new
                {
                    PaymentMethod = hasPayment && !string.IsNullOrWhiteSpace(p!.PaymentMethod) ? p!.PaymentMethod : "Unknown",
                    Currency = o.Currency,
                };
            })
            .Select(g => new TransactionSummaryRow(
                g.Key.PaymentMethod,
                g.Count(),
                g.Sum(x => x.Total),
                g.Key.Currency))
            .OrderByDescending(r => r.TotalAmount)
            .ToList();
    }

    public async Task<TransactionDetailPage> GetTransactionDetailsAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        TransactionDetailFilter filter,
        string? cursor, int limit,
        CancellationToken ct = default)
    {
        var lim = Math.Clamp(limit, 1, 100);
        var take = lim + 1;

        Guid cursorId = default;
        var hasCursor = !string.IsNullOrWhiteSpace(cursor) && Guid.TryParse(cursor, out cursorId);

        // DAI-XXX: brandCode now actually filters via EXISTS on OrderItems.VariantSnapshot->>'brandCode'.
        // PaymentType joined in from PaymentComponents (multi-tender), comma-joined when an order has multiple components.
        // Promotion arrays joined from OrderPromotions (EditorKind = 'order' for OrderPromoCode, 'item' for ItemPromoCodes).
        const string sql = """
            SELECT
                o."Id"                                            AS OrderId,
                NULL::text                                        AS ReceiptNumber, -- PR2 migration 023 will materialize this column
                o."CreatedAt"                                     AS Date,
                o."CustomerId"                                    AS Member,
                o."CustomerName"                                  AS CustomerName,
                o."CustomerEmail"                                 AS CustomerEmail,
                o."StoreId"                                       AS Store,
                o."Status"                                        AS Status,
                o."Total"                                         AS Amount,
                o."TaxTotal"                                      AS TaxAmount,
                o."OrderDiscount"                                 AS OrderDiscount,
                o."Currency"                                      AS Currency,
                o."PromoCode"                                     AS OrderPromoCode,
                o."ShippingAddress"->>'countryCode'               AS BillingCountry,
                COALESCE(
                    (SELECT array_agg(DISTINCT op."Name")
                     FROM "OrderPromotions" op
                     WHERE op."OrderId" = o."Id"::text AND op."TenantId" = o."TenantId"),
                    ARRAY[]::text[])                              AS Campaigns,
                COALESCE(
                    (SELECT array_agg(DISTINCT op."PromoCode")
                     FROM "OrderPromotions" op
                     WHERE op."OrderId" = o."Id"::text AND op."TenantId" = o."TenantId"
                       AND op."EditorKind" = 'item' AND op."PromoCode" IS NOT NULL),
                    ARRAY[]::text[])                              AS ItemPromoCodes,
                COALESCE(
                    (SELECT string_agg(DISTINCT pc."Type", ',' ORDER BY pc."Type")
                     FROM "OrderPayments" op2
                     INNER JOIN "PaymentComponents" pc ON pc."OrderPaymentId" = op2."Id"
                     WHERE op2."OrderId" = o."Id"),
                    '')                                           AS PaymentType
            FROM "Orders" o
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
              AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed')
              AND (@MemberQuery   IS NULL OR o."CustomerId" ILIKE '%' || @MemberQuery || '%'
                                          OR o."CustomerName" ILIKE '%' || @MemberQuery || '%'
                                          OR o."CustomerEmail" ILIKE '%' || @MemberQuery || '%')
              AND (@BrandCode     IS NULL OR EXISTS (
                       SELECT 1 FROM "OrderItems" oi
                       WHERE oi."OrderId" = o."Id"
                         AND oi."VariantSnapshot"->>'brandCode' = @BrandCode))
              AND (@OrderPromoCode IS NULL OR o."PromoCode" = @OrderPromoCode)
              AND (@ItemPromoCode  IS NULL OR EXISTS (
                       SELECT 1 FROM "OrderPromotions" op3
                       WHERE op3."OrderId" = o."Id"::text
                         AND op3."TenantId" = o."TenantId"
                         AND op3."EditorKind" = 'item'
                         AND op3."PromoCode" = @ItemPromoCode))
              AND (@BillingCountry IS NULL OR o."ShippingAddress"->>'countryCode' = @BillingCountry)
              AND (@PaymentType    IS NULL OR EXISTS (
                       SELECT 1 FROM "OrderPayments" op4
                       INNER JOIN "PaymentComponents" pc2 ON pc2."OrderPaymentId" = op4."Id"
                       WHERE op4."OrderId" = o."Id" AND pc2."Type" = @PaymentType))
              AND (@HasCursor = FALSE OR o."Id" < @CursorId::uuid)
            ORDER BY o."CreatedAt" DESC, o."Id" DESC
            LIMIT @Take
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var raw = (await conn.QueryAsync<TransactionDetailRaw>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                MemberQuery = NullIfBlank(filter.MemberQuery),
                BrandCode = NullIfBlank(filter.BrandCode),
                OrderPromoCode = NullIfBlank(filter.OrderPromoCode),
                ItemPromoCode = NullIfBlank(filter.ItemPromoCode),
                BillingCountry = NullIfBlank(filter.BillingCountry),
                PaymentType = NullIfBlank(filter.PaymentType),
                HasCursor = hasCursor,
                CursorId = hasCursor ? cursorId : (Guid?)null,
                Take = take,
            }, cancellationToken: ct)).ConfigureAwait(false)).ToList();

        var rows = raw.Select(r => new TransactionDetailRow(
            r.OrderId,
            r.ReceiptNumber,
            r.Date,
            r.Member,
            r.CustomerName,
            r.CustomerEmail,
            r.Store,
            r.Status,
            r.Amount,
            r.TaxAmount,
            r.OrderDiscount,
            r.Currency,
            r.OrderPromoCode,
            r.BillingCountry,
            r.Campaigns ?? Array.Empty<string>(),
            r.ItemPromoCodes ?? Array.Empty<string>(),
            r.PaymentType ?? string.Empty)).ToList();

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
        // DAI-XXX: Same cross-DB split — read orders, batch-load payments, in-process join + filter.
        const string ordersSql = """
            SELECT o."Id" AS OrderId
            FROM "Orders" o
            WHERE o."TenantId" = @TenantId
              AND (@StoreId = '' OR o."StoreId" = @StoreId)
              AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
            ORDER BY o."CreatedAt" DESC
            LIMIT 2000
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var orderIds = (await conn.QueryAsync<Guid>(
            new CommandDefinition(ordersSql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false)).ToList();

        if (orderIds.Count == 0)
            return Array.Empty<EcommercePaymentRow>();

        var paymentByOrder = await _paymentQueries.GetLatestByOrderIdsAsync(orderIds, ct).ConfigureAwait(false);
        var trimmedMethod = NullIfBlank(paymentMethod);

        var rows = new List<EcommercePaymentRow>(paymentByOrder.Count);
        foreach (var orderId in orderIds)
        {
            if (!paymentByOrder.TryGetValue(orderId, out var p)) continue;
            if (trimmedMethod is not null && !string.Equals(p.PaymentMethod, trimmedMethod, StringComparison.Ordinal))
                continue;
            rows.Add(new EcommercePaymentRow(
                orderId,
                p.PaymentMethod,
                p.Amount,
                p.Currency,
                p.PaymentIntentId,
                DateTime.UtcNow));
        }

        return rows.Take(500).ToList();
    }

    public async Task<TransactionsOverviewRow> GetTransactionsOverviewAsync(
        string tenantId, string storeId, DateOnly dateFrom, DateOnly dateTo,
        CancellationToken ct = default)
    {
        // DAI-XXX: 12-metric overview computed in a single pass.
        // Denominators (DistinctMonths, DistinctDays) are exposed so FE can show "avg over N days/months" transparently.
        // Excludes the same payment-failure statuses as the summary/details views for consistency.
        const string sql = """
            WITH base AS (
                SELECT o."Id", o."CreatedAt", o."Total", o."TaxTotal", o."OrderDiscount", o."CustomerId"
                FROM "Orders" o
                WHERE o."TenantId" = @TenantId
                  AND (@StoreId = '' OR o."StoreId" = @StoreId)
                  AND o."CreatedAt" >= @DateFrom AND o."CreatedAt" < @DateTo
                  AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed')
            ),
            by_day AS (
                SELECT date_trunc('day', "CreatedAt") AS d, COUNT(*) AS c, SUM("Total") AS t
                FROM base GROUP BY 1
            ),
            top_day AS (
                SELECT d, c, t FROM by_day ORDER BY t DESC LIMIT 1
            )
            SELECT
                COUNT(*)                                                AS TotalTransactions,
                COALESCE(SUM("Total"), 0)                               AS TotalAmount,
                COALESCE(SUM("TaxTotal"), 0)                            AS TotalTax,
                COALESCE(SUM("OrderDiscount"), 0)                       AS TotalDiscount,
                COUNT(DISTINCT "CustomerId")                            AS UniqueCustomers,
                COALESCE(AVG("Total"), 0)                               AS AverageOrderValue,
                (SELECT COUNT(DISTINCT date_trunc('month', "CreatedAt")) FROM base) AS DistinctMonths,
                (SELECT COUNT(DISTINCT date_trunc('day', "CreatedAt"))   FROM base) AS DistinctDays,
                (SELECT t FROM top_day)                                 AS TopDayAmount,
                (SELECT c FROM top_day)                                 AS TopDayTransactions,
                (SELECT d FROM top_day)                                 AS TopDayDate,
                CASE WHEN (SELECT COUNT(DISTINCT date_trunc('month', "CreatedAt")) FROM base) > 0
                     THEN COALESCE(SUM("Total"), 0) / (SELECT COUNT(DISTINCT date_trunc('month', "CreatedAt")) FROM base)
                     ELSE 0 END                                         AS AverageMonthlyAmount,
                CASE WHEN (SELECT COUNT(DISTINCT date_trunc('day', "CreatedAt")) FROM base) > 0
                     THEN COALESCE(SUM("Total"), 0) / (SELECT COUNT(DISTINCT date_trunc('day', "CreatedAt")) FROM base)
                     ELSE 0 END                                         AS AverageDailyAmount
            FROM base
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var raw = await conn.QuerySingleAsync<TransactionsOverviewRaw>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DateTo = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            }, cancellationToken: ct)).ConfigureAwait(false);

        return new TransactionsOverviewRow(
            (int)raw.TotalTransactions,
            raw.TotalAmount,
            raw.TotalTax,
            raw.TotalDiscount,
            (int)raw.UniqueCustomers,
            raw.AverageOrderValue,
            (int)raw.DistinctMonths,
            (int)raw.DistinctDays,
            raw.TopDayAmount,
            raw.TopDayTransactions.HasValue ? (int)raw.TopDayTransactions.Value : null,
            raw.TopDayDate,
            raw.AverageMonthlyAmount,
            raw.AverageDailyAmount);
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

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

internal sealed class TransactionDetailRaw
{
    public Guid OrderId { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime Date { get; set; }
    public string Member { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string Store { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal OrderDiscount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? OrderPromoCode { get; set; }
    public string? BillingCountry { get; set; }
    public string[]? Campaigns { get; set; }
    public string[]? ItemPromoCodes { get; set; }
    public string? PaymentType { get; set; }
}

internal sealed class TransactionsOverviewRaw
{
    public long TotalTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalDiscount { get; set; }
    public long UniqueCustomers { get; set; }
    public decimal AverageOrderValue { get; set; }
    public long DistinctMonths { get; set; }
    public long DistinctDays { get; set; }
    public decimal? TopDayAmount { get; set; }
    public long? TopDayTransactions { get; set; }
    public DateTime? TopDayDate { get; set; }
    public decimal AverageMonthlyAmount { get; set; }
    public decimal AverageDailyAmount { get; set; }
}

public sealed record TransactionDetailFilter(
    string? MemberQuery = null,
    string? BrandCode = null,
    string? PaymentMethod = null,
    string? OrderPromoCode = null,
    string? ItemPromoCode = null,
    string? BillingCountry = null,
    string? PaymentType = null);

public sealed record TransactionSummaryRow(string PaymentMethod, int TransactionCount, decimal TotalAmount, string Currency);

public sealed record TransactionDetailRow(
    Guid OrderId,
    string? ReceiptNumber,
    DateTime Date,
    string Member,
    string? CustomerName,
    string? CustomerEmail,
    string Store,
    string Status,
    decimal Amount,
    decimal TaxAmount,
    decimal OrderDiscount,
    string Currency,
    string? OrderPromoCode,
    string? BillingCountry,
    string[] Campaigns,
    string[] ItemPromoCodes,
    string PaymentType);

public sealed record TransactionDetailPage(IReadOnlyList<TransactionDetailRow> Items, string? NextCursor);

public sealed record EcommercePaymentRow(Guid OrderId, string PaymentMethod, decimal Amount, string Currency, string TransactionRef, DateTime Date);

public sealed record TransactionsOverviewRow(
    int TotalTransactions,
    decimal TotalAmount,
    decimal TotalTax,
    decimal TotalDiscount,
    int UniqueCustomers,
    decimal AverageOrderValue,
    int DistinctMonths,
    int DistinctDays,
    decimal? TopDayAmount,
    int? TopDayTransactions,
    DateTime? TopDayDate,
    decimal AverageMonthlyAmount,
    decimal AverageDailyAmount);

public sealed record SalesByCategoryRow(string Category, int ProductsCount, int Transactions, int UnitsSold, decimal TotalSales, string Currency);
public sealed record SalesByProductRow(string ProductId, string ProductNameJson, int UnitsSold, decimal TotalSales, string Currency);
public sealed record SalesByTenantRow(string TenantId, int OrdersCount, int ProductsSold, decimal TotalSales, string Currency);
