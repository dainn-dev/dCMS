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

    public async Task<IReadOnlyList<SalesRow>> GetSalesAsync(
        string tenantId,
        string storeId,
        DateOnly dateFrom,
        DateOnly dateTo,
        string groupBy,
        CancellationToken ct)
    {
        // Dapper doesn't support DateOnly by default; use DateTime and compare against DATE columns.
        var df = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dt = dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        string sql = groupBy switch
        {
            "store" => """
                SELECT
                    store_id AS Key,
                    SUM(orders_count)::bigint AS Orders,
                    SUM(gross_amount)::numeric AS Gross
                FROM analytics.orders_daily
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND date >= @DateFrom AND date < @DateTo
                GROUP BY store_id
                ORDER BY SUM(gross_amount) DESC
                """,
            "product" => """
                SELECT
                    product_id AS Key,
                    NULL::bigint AS Orders,
                    SUM(gross_amount)::numeric AS Gross
                FROM analytics.sales_by_product
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND date >= @DateFrom AND date < @DateTo
                GROUP BY product_id
                ORDER BY SUM(gross_amount) DESC
                LIMIT 500
                """,
            "category" => """
                SELECT
                    category_id::text AS Key,
                    NULL::bigint AS Orders,
                    SUM(gross_amount)::numeric AS Gross
                FROM analytics.sales_by_category
                WHERE tenant_id = @TenantId
                  AND (@StoreId = '' OR store_id = @StoreId)
                  AND date >= @DateFrom AND date < @DateTo
                GROUP BY category_id
                ORDER BY SUM(gross_amount) DESC
                LIMIT 500
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), "groupBy must be one of: store|product|category"),
        };

        await using var conn = new NpgsqlConnection(_analyticsCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SalesRow>(
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

    public async Task<IReadOnlyList<AbandonCartRow>> GetAbandonCartAsync(
        string tenantId,
        string storeId,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken ct)
    {
        // Abandoned = created in range and no confirm within 24h.
        const string sql = """
            WITH created AS (
                SELECT tenant_id, store_id, cart_id, MIN(occurred_at) AS created_at
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
            )
            SELECT c.cart_id AS CartId, c.created_at AS CreatedAt
            FROM created c
            LEFT JOIN confirmed x
              ON x.tenant_id = c.tenant_id AND x.store_id = c.store_id AND x.cart_id = c.cart_id
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

    public async Task<IReadOnlyList<RestockSubscriptionRow>> GetRestockSubscriptionsAsync(
        string tenantId,
        string storeId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                product_id AS ProductId,
                COUNT(*)::int AS Subscriptions,
                COUNT(fulfilled_at)::int AS Fulfilled
            FROM analytics.restock_subscriptions
            WHERE tenant_id = @TenantId
              AND (@StoreId = '' OR store_id = @StoreId)
            GROUP BY product_id
            ORDER BY COUNT(*) DESC
            LIMIT 1000
            """;

        await using var conn = new NpgsqlConnection(_analyticsCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<RestockSubscriptionRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public sealed record SalesRow(string Key, long? Orders, decimal Gross);
    public sealed record AbandonCartRow(string CartId, DateTime CreatedAt);
    public sealed record RestockSubscriptionRow(string ProductId, int Subscriptions, int Fulfilled);
}

