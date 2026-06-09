using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Reports.Api.Routes;

public sealed class AnalyticsReportQueryStore
{
    private readonly string _analyticsCs;

    public AnalyticsReportQueryStore(IConfiguration configuration)
    {
        _analyticsCs = configuration.GetConnectionString("Analytics")
            ?? throw new InvalidOperationException("ConnectionStrings:Analytics is required.");
    }

    /// <summary>
    /// Sales Reports BRD §1–§3: aggregate sales by one of the three report dimensions
    /// (category | brand | product). Returns BRD-shaped rows; columns that the analytics
    /// projection doesn't yet carry (display name, UPC/SKU, "Current No. of Products",
    /// per-dimension transaction count) are returned NULL and degrade to "—" in the UI —
    /// the same graceful-degradation precedent as the delivery-slots/restock reports.
    /// </summary>
    public async Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
        string tenantId,
        string storeId,
        DateOnly dateFrom,
        DateOnly dateTo,
        string groupBy,
        CancellationToken ct)
    {
        // BRD §2 "Sales by Brand": analytics has no brand dimension yet (no brand_id on any
        // rollup table), so the report ships ahead of its projection and returns no rows.
        if (groupBy == "brand")
            return Array.Empty<SalesReportRow>();

        // Dapper doesn't support DateOnly by default; use DateTime and compare against DATE columns.
        var df = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dt = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        string sql = groupBy switch
        {
            // BRD §1 Sales by Category — Products Sold = units, Total Category Sales = gross.
            "category" => """
                SELECT
                    category_id::text         AS Key,
                    NULL::text                AS Name,
                    NULL::text                AS Upc,
                    NULL::text                AS Sku,
                    NULL::int                 AS ProductsCount,
                    NULL::bigint              AS Transactions,
                    SUM(units_sold)::bigint   AS UnitsSold,
                    SUM(gross_amount)::numeric AS Gross,
                    'SGD'                     AS Currency
                FROM analytics.sales_by_category
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND date >= @DateFrom AND date < @DateTo
                GROUP BY category_id
                ORDER BY SUM(gross_amount) DESC
                LIMIT 500
                """,
            // BRD §3 Sales by Product — Total Products Sold = units, Total Product Sales = gross.
            "product" => """
                SELECT
                    product_id                AS Key,
                    NULL::text                AS Name,
                    NULL::text                AS Upc,
                    NULL::text                AS Sku,
                    NULL::int                 AS ProductsCount,
                    NULL::bigint              AS Transactions,
                    SUM(units_sold)::bigint   AS UnitsSold,
                    SUM(gross_amount)::numeric AS Gross,
                    'SGD'                     AS Currency
                FROM analytics.sales_by_product
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND date >= @DateFrom AND date < @DateTo
                GROUP BY product_id
                ORDER BY SUM(gross_amount) DESC
                LIMIT 500
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), "groupBy must be one of: category|brand|product"),
        };

        await using var conn = new NpgsqlConnection(_analyticsCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SalesReportRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                StoreId = storeId,
                DateFrom = df,
                DateTo = dt,
            }, cancellationToken: ct)).ConfigureAwait(false);

        // note: date columns are DATE type; keep DateOnly parameters.
        return rows.ToList();
    }

    /// <summary>
    /// Sales Reports BRD §3 "Overview Section": summary metrics for the Sales by Product report.
    /// BR05 — these must match the sum of the displayed product rows, so Total Products / Total
    /// Sales / Total Products Sold are computed over the same sales_by_product scope. Total Sales
    /// Transactions uses the orders_daily rollup (the best transaction-count proxy in analytics).
    /// </summary>
    public async Task<SalesOverview> GetSalesProductOverviewAsync(
        string tenantId,
        string storeId,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken ct)
    {
        var df = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dt = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        const string sql = """
            SELECT
                (SELECT COUNT(DISTINCT product_id)::bigint
                   FROM analytics.sales_by_product
                   WHERE tenant_id = @TenantId AND (@StoreId = '' OR store_id = @StoreId)
                     AND date >= @DateFrom AND date < @DateTo)              AS TotalProducts,
                (SELECT COALESCE(SUM(gross_amount), 0)::numeric
                   FROM analytics.sales_by_product
                   WHERE tenant_id = @TenantId AND (@StoreId = '' OR store_id = @StoreId)
                     AND date >= @DateFrom AND date < @DateTo)              AS TotalSales,
                (SELECT COALESCE(SUM(orders_count), 0)::bigint
                   FROM analytics.orders_daily
                   WHERE tenant_id = @TenantId AND (@StoreId = '' OR store_id = @StoreId)
                     AND date >= @DateFrom AND date < @DateTo)              AS TotalSalesTransactions,
                (SELECT COALESCE(SUM(units_sold), 0)::bigint
                   FROM analytics.sales_by_product
                   WHERE tenant_id = @TenantId AND (@StoreId = '' OR store_id = @StoreId)
                     AND date >= @DateFrom AND date < @DateTo)              AS TotalProductsSold,
                'SGD'                                                       AS Currency
            """;

        await using var conn = new NpgsqlConnection(_analyticsCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.QuerySingleAsync<SalesOverview>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            StoreId = storeId,
            DateFrom = df,
            DateTo = dt,
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AbandonCartRow>> GetAbandonCartAsync(
        string tenantId,
        string storeId,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken ct)
    {
        // Abandon Cart BRD: one row per abandoned cart (BR03). Abandoned = created in range and not
        // confirmed within 24h (BR01/BR02). Customer/value/product-count come from the latest 'created'
        // event payload (BR04/BR05); reminder-email stats aggregate the 'reminder_sent' events (BR06/BR07).
        const string sql = """
            WITH created AS (
                SELECT tenant_id, store_id, cart_id,
                       MIN(occurred_at) AS created_at,
                       (ARRAY_AGG(payload ORDER BY occurred_at DESC))[1] AS payload
                FROM analytics.cart_events
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND event_type = 'created'
                  AND occurred_at >= @From AND occurred_at < @To
                GROUP BY tenant_id, store_id, cart_id
            ),
            confirmed AS (
                SELECT tenant_id, store_id, cart_id, MIN(occurred_at) AS confirmed_at
                FROM analytics.cart_events
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND event_type = 'confirmed'
                GROUP BY tenant_id, store_id, cart_id
            ),
            emails AS (
                SELECT tenant_id, store_id, cart_id,
                       COUNT(*) AS email_sent_count,
                       MAX(occurred_at) AS last_email_sent_at
                FROM analytics.cart_events
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND event_type = 'reminder_sent'
                GROUP BY tenant_id, store_id, cart_id
            )
            SELECT c.cart_id                                          AS CartId,
                   c.payload->>'customerName'                        AS CustomerName,
                   c.payload->>'customerEmail'                       AS CustomerEmail,
                   COALESCE((c.payload->>'cartValue')::numeric, 0)   AS CartValue,
                   COALESCE(c.payload->>'currency', 'SGD')           AS Currency,
                   COALESCE((c.payload->>'productCount')::int, 0)    AS ProductCount,
                   COALESCE(e.email_sent_count, 0)::int              AS EmailSentCount,
                   e.last_email_sent_at                              AS LastEmailSentAt,
                   c.created_at                                      AS CreatedAt
            FROM created c
            LEFT JOIN confirmed x
              ON x.tenant_id = c.tenant_id AND x.store_id = c.store_id AND x.cart_id = c.cart_id
            LEFT JOIN emails e
              ON e.tenant_id = c.tenant_id AND e.store_id = c.store_id AND e.cart_id = c.cart_id
            WHERE x.confirmed_at IS NULL OR x.confirmed_at > c.created_at + INTERVAL '24 hours'
            ORDER BY c.created_at DESC
            LIMIT 1000
            """;

        await using var conn = new NpgsqlConnection(_analyticsCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<AbandonCartRow>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            StoreId = storeId,
            From = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            To = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    /// <summary>
    /// Restock Notifications Subscriptions BRD §3: one row per subscription (BR03) with the detail
    /// fields the operator searches on. BR01: subscription-date range is required. BR02: UPC/SKU/
    /// product-name/email are optional substring filters.
    /// </summary>
    public async Task<IReadOnlyList<RestockSubscriptionDetailRow>> GetRestockSubscriptionDetailsAsync(
        string tenantId,
        string storeId,
        DateOnly dateFrom,
        DateOnly dateTo,
        string? upc,
        string? sku,
        string? productName,
        string? email,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                product_id   AS ProductId,
                upc          AS Upc,
                sku          AS Sku,
                product_name AS ProductName,
                email        AS Email,
                created_at   AS SubscriptionDate,
                fulfilled_at AS RestockNotificationSentOn
            FROM analytics.restock_subscriptions
            WHERE tenant_id = @TenantId
              AND (@StoreId = '' OR store_id = @StoreId)
              AND created_at >= @From AND created_at < @To
              AND (@Upc = ''         OR upc          ILIKE @UpcLike)
              AND (@Sku = ''         OR sku          ILIKE @SkuLike)
              AND (@ProductName = '' OR product_name ILIKE @ProductNameLike)
              AND (@Email = ''       OR email        ILIKE @EmailLike)
            ORDER BY created_at DESC
            LIMIT 1000
            """;

        // Trim filters; empty string disables the corresponding predicate. ILIKE params are wrapped
        // as %term% for substring search (parameterised — no injection risk).
        var u = (upc ?? string.Empty).Trim();
        var s = (sku ?? string.Empty).Trim();
        var p = (productName ?? string.Empty).Trim();
        var e = (email ?? string.Empty).Trim();

        await using var conn = new NpgsqlConnection(_analyticsCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<RestockSubscriptionDetailRow>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            StoreId = storeId,
            From = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            To = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Upc = u,
            Sku = s,
            ProductName = p,
            Email = e,
            UpcLike = $"%{u}%",
            SkuLike = $"%{s}%",
            ProductNameLike = $"%{p}%",
            EmailLike = $"%{e}%",
        }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    /// <summary>
    /// BRD-shaped sales row. <paramref name="Key"/> is the dimension identifier (category id /
    /// brand code / product id); <paramref name="Name"/>, <paramref name="Upc"/>, <paramref name="Sku"/>,
    /// <paramref name="ProductsCount"/> and <paramref name="Transactions"/> are NULL until the analytics
    /// projection carries them and render as "—" in the UI.
    /// </summary>
    public sealed record SalesReportRow(
        string Key,
        string? Name,
        string? Upc,
        string? Sku,
        int? ProductsCount,
        long? Transactions,
        long UnitsSold,
        decimal Gross,
        string Currency);

    /// <summary>Sales by Product overview metrics (BRD §3 Overview Section).</summary>
    public sealed record SalesOverview(
        long TotalProducts,
        decimal TotalSales,
        long TotalSalesTransactions,
        long TotalProductsSold,
        string Currency);

    /// <summary>Matches the frontend contract: { cartId, customerName, customerEmail, cartValue, currency, productCount, emailSentCount, lastEmailSentAt, createdAt }.</summary>
    public sealed record AbandonCartRow(
        string CartId,
        string? CustomerName,
        string? CustomerEmail,
        decimal CartValue,
        string Currency,
        int ProductCount,
        int EmailSentCount,
        DateTime? LastEmailSentAt,
        DateTime CreatedAt);

    /// <summary>Matches the frontend contract: { productId, upc, sku, productName, email, subscriptionDate, restockNotificationSentOn }.</summary>
    public sealed record RestockSubscriptionDetailRow(
        string ProductId,
        string? Upc,
        string? Sku,
        string? ProductName,
        string? Email,
        DateTime SubscriptionDate,
        DateTime? RestockNotificationSentOn);
}

