using Dapper;
using dCMS.Reports.Api.Routes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Reports;

/// <summary>
/// DAI-711: AnalyticsReportQueryStore integration tests.
/// Spins up a fresh PostgreSQL container, applies the analytics migration,
/// seeds rows directly, and verifies the query store returns the expected aggregates.
/// </summary>
public sealed class AnalyticsReportQueryStoreIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string? _cs;
    private bool _ready;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_analytics_itest")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();
            await _postgres.StartAsync().ConfigureAwait(false);
            _cs = _postgres.GetConnectionString();

            var sql = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Migrations", "Analytics", "001_CreateAnalyticsTables.sql"))
                .ConfigureAwait(false);

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync().ConfigureAwait(false);
            await conn.ExecuteAsync(sql).ConfigureAwait(false);

            _ready = true;
        }
        catch
        {
            _ready = false;
            if (_postgres is not null) { await _postgres.DisposeAsync().ConfigureAwait(false); _postgres = null; }
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync().ConfigureAwait(false);
        _postgres = null;
    }

    private void Skip() => Xunit.Skip.IfNot(_ready && _cs is not null, "Docker / Testcontainers not available.");

    private AnalyticsReportQueryStore BuildStore()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Analytics"] = _cs })
            .Build();
        return new AnalyticsReportQueryStore(cfg);
    }

    [SkippableFact]
    public async Task GetSales_groupBy_category_aggregates_within_range()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.sales_by_category").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.sales_by_category (tenant_id, store_id, category_id, date, units_sold, gross_amount)
            VALUES
              ('t1', 's1', 10, '2026-04-10', 5,  500),
              ('t1', 's1', 10, '2026-04-12', 3,  300),
              ('t1', 's2', 20, '2026-04-11', 2,  200),
              ('t1', 's1', 10, '2026-03-01', 9,  999),  -- outside range
              ('t2', 's1', 10, '2026-04-11', 7,  777);  -- different tenant
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetSalesAsync(
            tenantId: "t1", storeId: "",
            dateFrom: new DateOnly(2026, 4, 1),
            dateTo: new DateOnly(2026, 4, 30),
            groupBy: "category",
            ct: CancellationToken.None);

        rows.Should().HaveCount(2);
        var cat10 = rows.First(r => r.Key == "10");
        cat10.UnitsSold.Should().Be(8);                    // Products Sold (BRD §1)
        cat10.Gross.Should().Be(800m);                     // Total Category Sales
        cat10.Transactions.Should().BeNull();              // not yet carried by the projection
        cat10.ProductsCount.Should().BeNull();
        rows.First(r => r.Key == "20").Gross.Should().Be(200m);
    }

    [SkippableFact]
    public async Task GetSales_groupBy_category_filters_by_storeId()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.sales_by_category").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.sales_by_category (tenant_id, store_id, category_id, date, units_sold, gross_amount)
            VALUES
              ('t1', 's1', 10, '2026-04-10', 5, 500),
              ('t1', 's2', 20, '2026-04-10', 5, 500);
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetSalesAsync(
            tenantId: "t1", storeId: "s1",
            dateFrom: new DateOnly(2026, 4, 1),
            dateTo: new DateOnly(2026, 4, 30),
            groupBy: "category",
            ct: CancellationToken.None);

        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be("10");
    }

    [SkippableFact]
    public async Task GetSales_groupBy_product_aggregates()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.sales_by_product").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.sales_by_product (tenant_id, store_id, product_id, date, units_sold, gross_amount)
            VALUES
              ('t1', 's1', 'p1', '2026-04-10', 2, 100),
              ('t1', 's1', 'p1', '2026-04-11', 3, 200),
              ('t1', 's1', 'p2', '2026-04-12', 1,  50);
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetSalesAsync(
            tenantId: "t1", storeId: "",
            dateFrom: new DateOnly(2026, 4, 1),
            dateTo: new DateOnly(2026, 4, 30),
            groupBy: "product",
            ct: CancellationToken.None);

        rows.Should().HaveCount(2);
        rows[0].Key.Should().Be("p1");                     // ordered by gross desc
        rows[0].Gross.Should().Be(300m);                   // Total Product Sales (BRD §3)
        rows[0].UnitsSold.Should().Be(5);                  // Total Products Sold
        rows[1].Key.Should().Be("p2");
    }

    [SkippableFact]
    public async Task GetSales_groupBy_brand_returns_empty_until_projection_exists()
    {
        Skip();
        // BRD §2: analytics has no brand dimension yet, so the report returns no rows.
        var rows = await BuildStore().GetSalesAsync(
            "t1", "", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), "brand", CancellationToken.None);
        rows.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task GetSalesProductOverview_matches_displayed_totals()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.sales_by_product").ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.orders_daily").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.sales_by_product (tenant_id, store_id, product_id, date, units_sold, gross_amount)
            VALUES
              ('t1', 's1', 'p1', '2026-04-10', 2, 100),
              ('t1', 's1', 'p1', '2026-04-11', 3, 200),
              ('t1', 's1', 'p2', '2026-04-12', 1,  50),
              ('t1', 's1', 'p3', '2026-03-01', 9, 999);  -- outside range
            """).ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.orders_daily (tenant_id, store_id, date, orders_count, gross_amount)
            VALUES
              ('t1', 's1', '2026-04-10', 4, 300),
              ('t1', 's1', '2026-04-12', 2,  50);
            """).ConfigureAwait(false);

        var ov = await BuildStore().GetSalesProductOverviewAsync(
            "t1", "", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), CancellationToken.None);

        ov.TotalProducts.Should().Be(2);             // p1, p2 (p3 outside range)
        ov.TotalProductsSold.Should().Be(6);         // 2 + 3 + 1
        ov.TotalSales.Should().Be(350m);             // 100 + 200 + 50
        ov.TotalSalesTransactions.Should().Be(6);    // orders_daily 4 + 2
    }

    [SkippableFact]
    public async Task GetSales_invalid_groupBy_throws()
    {
        Skip();
        await BuildStore().Invoking(s => s.GetSalesAsync(
            "t1", "", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), "store", CancellationToken.None))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [SkippableFact]
    public async Task GetAbandonCart_returns_carts_without_confirmation_within_24h()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.cart_events").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.cart_events (tenant_id, store_id, cart_id, event_type, occurred_at, payload)
            VALUES
              -- abandoned: created (with detail payload) and never confirmed; 2 reminder emails sent
              ('t1', 's1', 'cart-abandon', 'created',       '2026-04-10T08:00:00Z',
                 '{"customerName":"John Smith","customerEmail":"john@x.com","cartValue":324.00,"currency":"SGD","productCount":2}'),
              ('t1', 's1', 'cart-abandon', 'reminder_sent', '2026-04-10T10:00:00Z', NULL),
              ('t1', 's1', 'cart-abandon', 'reminder_sent', '2026-04-11T10:00:00Z', NULL),
              -- confirmed within 24h: NOT abandoned
              ('t1', 's1', 'cart-quick',   'created',       '2026-04-11T08:00:00Z', NULL),
              ('t1', 's1', 'cart-quick',   'confirmed',     '2026-04-11T09:00:00Z', NULL),
              -- confirmed >24h after creation: still considered abandoned
              ('t1', 's1', 'cart-late',    'created',       '2026-04-12T08:00:00Z', NULL),
              ('t1', 's1', 'cart-late',    'confirmed',     '2026-04-14T08:00:00Z', NULL);
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetAbandonCartAsync(
            "t1", "",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            CancellationToken.None);

        rows.Select(r => r.CartId).Should().BeEquivalentTo(new[] { "cart-abandon", "cart-late" });

        var enriched = rows.First(r => r.CartId == "cart-abandon");
        enriched.CustomerName.Should().Be("John Smith");
        enriched.CustomerEmail.Should().Be("john@x.com");
        enriched.CartValue.Should().Be(324.00m);          // BR04
        enriched.ProductCount.Should().Be(2);             // BR05
        enriched.EmailSentCount.Should().Be(2);           // BR06
        enriched.LastEmailSentAt.Should().NotBeNull();    // BR07

        // No 'created' payload and no reminders → safe defaults.
        var late = rows.First(r => r.CartId == "cart-late");
        late.CartValue.Should().Be(0m);
        late.EmailSentCount.Should().Be(0);
        late.LastEmailSentAt.Should().BeNull();
    }

    [SkippableFact]
    public async Task GetRestockSubscriptionDetails_returns_per_subscription_rows_filtered()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.restock_subscriptions").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.restock_subscriptions
                (tenant_id, store_id, product_id, customer_id, created_at, fulfilled_at, upc, sku, product_name, email)
            VALUES
              ('t1', 's1', 'p1', 'c1', '2026-04-10T08:00:00Z', NULL,                   '0001', 'SKU-1', 'Widget',  'a@x.com'),
              ('t1', 's1', 'p1', 'c2', '2026-04-11T08:00:00Z', '2026-04-12T08:00:00Z', '0001', 'SKU-1', 'Widget',  'b@x.com'),
              ('t1', 's2', 'p2', 'c3', '2026-04-12T08:00:00Z', NULL,                   '0002', 'SKU-2', 'Gadget',  'c@x.com'),
              ('t1', 's1', 'p3', 'c4', '2026-03-01T08:00:00Z', NULL,                   '0003', 'SKU-3', 'OldItem', 'd@x.com');
            """).ConfigureAwait(false);

        // BR01 date range filters out the March row; BR02 product-name filter narrows to "Widget".
        var rows = await BuildStore().GetRestockSubscriptionDetailsAsync(
            "t1", "", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            upc: null, sku: null, productName: "widget", email: null, CancellationToken.None);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.ProductName == "Widget");
        rows.Should().Contain(r => r.Email == "b@x.com" && r.RestockNotificationSentOn != null);
        rows.Should().Contain(r => r.Email == "a@x.com" && r.RestockNotificationSentOn == null);
    }
}
