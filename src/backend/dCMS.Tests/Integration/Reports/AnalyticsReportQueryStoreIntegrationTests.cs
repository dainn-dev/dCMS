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
    public async Task GetSales_groupBy_store_aggregates_within_range()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.orders_daily").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.orders_daily (tenant_id, store_id, date, orders_count, gross_amount)
            VALUES
              ('t1', 's1', '2026-04-10', 5,  500),
              ('t1', 's1', '2026-04-12', 3,  300),
              ('t1', 's2', '2026-04-11', 2,  200),
              ('t1', 's1', '2026-03-01', 9,  999),  -- outside range
              ('t2', 's1', '2026-04-11', 7,  777);  -- different tenant
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetSalesAsync(
            tenantId: "t1", storeId: "",
            dateFrom: new DateOnly(2026, 4, 1),
            dateTo: new DateOnly(2026, 4, 30),
            groupBy: "store",
            ct: CancellationToken.None);

        rows.Should().HaveCount(2);
        rows.First(r => r.Key == "s1").Orders.Should().Be(8);
        rows.First(r => r.Key == "s1").Gross.Should().Be(800m);
        rows.First(r => r.Key == "s2").Gross.Should().Be(200m);
    }

    [SkippableFact]
    public async Task GetSales_groupBy_store_filters_by_storeId()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.orders_daily").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.orders_daily (tenant_id, store_id, date, orders_count, gross_amount)
            VALUES
              ('t1', 's1', '2026-04-10', 5, 500),
              ('t1', 's2', '2026-04-10', 5, 500);
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetSalesAsync(
            tenantId: "t1", storeId: "s1",
            dateFrom: new DateOnly(2026, 4, 1),
            dateTo: new DateOnly(2026, 4, 30),
            groupBy: "store",
            ct: CancellationToken.None);

        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be("s1");
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
        rows[0].Gross.Should().Be(300m);
        rows[0].Orders.Should().BeNull();                  // product/category groupings don't expose order count
        rows[1].Key.Should().Be("p2");
    }

    [SkippableFact]
    public async Task GetSales_invalid_groupBy_throws()
    {
        Skip();
        await BuildStore().Invoking(s => s.GetSalesAsync(
            "t1", "", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), "tenant", CancellationToken.None))
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
            INSERT INTO analytics.cart_events (tenant_id, store_id, cart_id, event_type, occurred_at)
            VALUES
              -- abandoned: created and never confirmed
              ('t1', 's1', 'cart-abandon', 'created',   '2026-04-10T08:00:00Z'),
              -- confirmed within 24h: NOT abandoned
              ('t1', 's1', 'cart-quick',   'created',   '2026-04-11T08:00:00Z'),
              ('t1', 's1', 'cart-quick',   'confirmed', '2026-04-11T09:00:00Z'),
              -- confirmed >24h after creation: still considered abandoned
              ('t1', 's1', 'cart-late',    'created',   '2026-04-12T08:00:00Z'),
              ('t1', 's1', 'cart-late',    'confirmed', '2026-04-14T08:00:00Z');
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetAbandonCartAsync(
            "t1", "",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            CancellationToken.None);

        rows.Select(r => r.CartId).Should().BeEquivalentTo(new[] { "cart-abandon", "cart-late" });
    }

    [SkippableFact]
    public async Task GetRestockSubscriptions_aggregates_by_product()
    {
        Skip();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM analytics.restock_subscriptions").ConfigureAwait(false);
        await conn.ExecuteAsync("""
            INSERT INTO analytics.restock_subscriptions (tenant_id, store_id, product_id, customer_id, created_at, fulfilled_at)
            VALUES
              ('t1', 's1', 'p1', 'c1', '2026-04-10T08:00:00Z', NULL),
              ('t1', 's1', 'p1', 'c2', '2026-04-11T08:00:00Z', '2026-04-12T08:00:00Z'),
              ('t1', 's2', 'p1', 'c3', '2026-04-12T08:00:00Z', NULL),
              ('t1', 's1', 'p2', 'c4', '2026-04-12T08:00:00Z', '2026-04-13T08:00:00Z');
            """).ConfigureAwait(false);

        var rows = await BuildStore().GetRestockSubscriptionsAsync("t1", "", CancellationToken.None);

        rows.Should().HaveCount(2);
        rows.First(r => r.ProductId == "p1").Subscriptions.Should().Be(3);
        rows.First(r => r.ProductId == "p1").Fulfilled.Should().Be(1);
        rows.First(r => r.ProductId == "p2").Subscriptions.Should().Be(1);
        rows.First(r => r.ProductId == "p2").Fulfilled.Should().Be(1);
    }
}
