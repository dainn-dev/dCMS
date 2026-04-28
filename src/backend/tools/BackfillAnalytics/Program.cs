using Dapper;
using Npgsql;

// DAI-710: One-shot backfill from OLTP Order DB into Analytics DB.
// Usage:
//   dotnet run --project tools/BackfillAnalytics -- \
//     --order "<order-conn>" --analytics "<analytics-conn>" [--catalog "<catalog-conn>"] [--tenant t1] [--store s1] [--from 2026-01-01] [--to 2026-01-31]

var argsMap = ParseArgs(args);
var orderCs = Required(argsMap, "--order");
var analyticsCs = Required(argsMap, "--analytics");
var catalogCs = Optional(argsMap, "--catalog");
var tenant = Optional(argsMap, "--tenant");
var store = Optional(argsMap, "--store");
var from = Optional(argsMap, "--from");
var to = Optional(argsMap, "--to");

DateOnly? df = null;
DateOnly? dt = null;
if (!string.IsNullOrWhiteSpace(from) && DateOnly.TryParse(from, out var f)) df = f;
if (!string.IsNullOrWhiteSpace(to) && DateOnly.TryParse(to, out var t)) dt = t;

await using var order = new NpgsqlConnection(orderCs);
await using var analytics = new NpgsqlConnection(analyticsCs);
await order.OpenAsync();
await analytics.OpenAsync();

// Ensure tables exist (worker migration should be run first; this is a safety net).
await analytics.ExecuteAsync("CREATE SCHEMA IF NOT EXISTS analytics;");

Console.WriteLine("Backfill starting...");

var orders = await order.QueryAsync<OrderRow>(
    """
    SELECT o."Id"::text AS OrderId, o."TenantId" AS TenantId, o."StoreId" AS StoreId, o."Total" AS Total, o."CreatedAt" AS CreatedAt
    FROM "Orders" o
    WHERE (@TenantId IS NULL OR o."TenantId" = @TenantId)
      AND (@StoreId IS NULL OR o."StoreId" = @StoreId)
      AND (@From IS NULL OR o."CreatedAt" >= @From)
      AND (@To IS NULL OR o."CreatedAt" < @To)
      AND o."Status" NOT IN ('PaymentPending', 'PaymentFailed', 'AuthFailed', 'Cancelled', 'AdminCancelled', 'UserCancelled')
    ORDER BY o."CreatedAt" ASC
    """,
    new
    {
        TenantId = tenant,
        StoreId = store,
        From = df.HasValue ? df.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : (DateTime?)null,
        To = dt.HasValue ? dt.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : (DateTime?)null,
    });

var totalOrders = 0;
foreach (var o in orders)
{
    totalOrders++;
    var d = DateOnly.FromDateTime(o.CreatedAt.ToUniversalTime());

    await analytics.ExecuteAsync(
        """
        INSERT INTO analytics.orders_daily
            (tenant_id, store_id, date, orders_count, gross_amount, net_amount, refund_count, refund_amount)
        VALUES
            (@TenantId, @StoreId, @Date, 1, @Gross, @Net, 0, 0)
        ON CONFLICT (tenant_id, store_id, date)
        DO UPDATE SET
            orders_count = analytics.orders_daily.orders_count + 1,
            gross_amount = analytics.orders_daily.gross_amount + EXCLUDED.gross_amount,
            net_amount   = analytics.orders_daily.net_amount   + EXCLUDED.net_amount
        """,
        new
        {
            TenantId = o.TenantId,
            StoreId = o.StoreId,
            Date = d,
            Gross = o.Total,
            Net = o.Total,
        });

    var items = await order.QueryAsync<OrderItemRow>(
        """
        SELECT "ProductId" AS ProductId, "Quantity" AS Quantity, "LineTotal" AS LineTotal
        FROM "OrderItems"
        WHERE "OrderId" = @OrderId::uuid
        """,
        new { OrderId = o.OrderId });

    foreach (var g in items.GroupBy(i => i.ProductId, StringComparer.Ordinal))
    {
        var units = g.Sum(x => x.Quantity);
        var gross = g.Sum(x => x.LineTotal);

        await analytics.ExecuteAsync(
            """
            INSERT INTO analytics.sales_by_product
                (tenant_id, store_id, product_id, date, units_sold, gross_amount)
            VALUES
                (@TenantId, @StoreId, @ProductId, @Date, @Units, @Gross)
            ON CONFLICT (tenant_id, store_id, product_id, date)
            DO UPDATE SET
                units_sold = analytics.sales_by_product.units_sold + EXCLUDED.units_sold,
                gross_amount = analytics.sales_by_product.gross_amount + EXCLUDED.gross_amount
            """,
            new
            {
                TenantId = o.TenantId,
                StoreId = o.StoreId,
                ProductId = g.Key,
                Date = d,
                Units = units,
                Gross = gross,
            });

        if (!string.IsNullOrWhiteSpace(catalogCs))
        {
            await using var cat = new NpgsqlConnection(catalogCs);
            await cat.OpenAsync();
            var categoryId = await cat.ExecuteScalarAsync<int?>(
                """
                SELECT "CategoryId" FROM "Products" WHERE "Id" = @Id
                """,
                new { Id = g.Key });

            if (categoryId.HasValue)
            {
                await analytics.ExecuteAsync(
                    """
                    INSERT INTO analytics.sales_by_category
                        (tenant_id, store_id, category_id, date, units_sold, gross_amount)
                    VALUES
                        (@TenantId, @StoreId, @CategoryId, @Date, @Units, @Gross)
                    ON CONFLICT (tenant_id, store_id, category_id, date)
                    DO UPDATE SET
                        units_sold = analytics.sales_by_category.units_sold + EXCLUDED.units_sold,
                        gross_amount = analytics.sales_by_category.gross_amount + EXCLUDED.gross_amount
                    """,
                    new
                    {
                        TenantId = o.TenantId,
                        StoreId = o.StoreId,
                        CategoryId = categoryId.Value,
                        Date = d,
                        Units = units,
                        Gross = gross,
                    });
            }
        }
    }

    if (totalOrders % 500 == 0)
        Console.WriteLine($"Backfilled {totalOrders} orders...");
}

Console.WriteLine($"Backfill done. Orders processed: {totalOrders}");

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var k = args[i];
        if (!k.StartsWith("--", StringComparison.Ordinal)) continue;
        var v = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) ? args[i + 1] : "true";
        map[k] = v;
        if (v != "true") i++;
    }
    return map;
}

static string Required(Dictionary<string, string> map, string key) =>
    map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
        ? v
        : throw new InvalidOperationException($"Missing required arg {key}");

static string? Optional(Dictionary<string, string> map, string key) =>
    map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

file sealed record OrderRow(string OrderId, string TenantId, string StoreId, decimal Total, DateTime CreatedAt);
file sealed record OrderItemRow(string ProductId, int Quantity, decimal LineTotal);

