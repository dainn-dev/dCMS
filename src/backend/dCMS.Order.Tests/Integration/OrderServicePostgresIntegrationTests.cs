using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using OrderDomain = dCMS.Order.Core.Domain;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Services;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>DAI-313 — Order persistence + outbox against real PostgreSQL (Testcontainers).</summary>
public sealed class OrderServicePostgresIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IOrderService? _orderService;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dcms_order_itest")
            .WithUsername("dcms")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync();

        var connectionString = _postgres.GetConnectionString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Order"] = connectionString,
            })
            .Build();

        OrderDatabaseUpgrader.Run(configuration, NullLogger.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IInventoryClient, NoOpInventoryClient>();
        services.AddSingleton<IPaymentClient, FakePaymentClient>();
        services.AddSingleton<OrderQueryStore>();
        services.AddSingleton<IOrderService, OrderService>();
        _orderService = services.BuildServiceProvider().GetRequiredService<IOrderService>();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task CreateOrder_persists_order_items_and_OrderPlaced_outbox()
    {
        var orderId = Guid.NewGuid().ToString();
        var lineId = Guid.NewGuid().ToString("N")[..16];
        var cmd = new CreateOrderCommand(
            orderId,
            "t1",
            "s1",
            "cust-1",
            $"idem-{Guid.NewGuid():N}",
            [
                new CreateOrderLine(
                    lineId,
                    "prod-1",
                    "var-1",
                    "wh-itest",
                    2,
                    new OrderDomain.Money(9.99m, "USD"),
                    "Widget",
                    """{"sku":"W-1"}"""),
            ],
            new OrderDomain.ShippingAddress("1 Main", null, "HCMC", "SG", "700000", "VN"),
            DateTimeOffset.Parse("2026-04-12T12:00:00Z"));

        var created = await _orderService!.CreateOrderAsync(cmd);
        Assert.Equal(orderId, created.Order.Id);
        Assert.NotNull(created.PaymentUrl);
        Assert.StartsWith("https://pay.test/", created.PaymentUrl, StringComparison.Ordinal);
        Assert.Equal(OrderDomain.OrderStatus.PaymentPending, created.Order.Status);
        Assert.Equal("pi_test_intent", created.Order.PaymentIntentId);

        var loaded = await _orderService.GetByIdAsync("t1", "s1", orderId);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Items);
        Assert.Equal("Widget", loaded.Items[0].ProductNameSnapshot);
        Assert.Equal("pi_test_intent", loaded.PaymentIntentId);

        await using var conn = new NpgsqlConnection(_postgres!.GetConnectionString());
        await conn.OpenAsync();
        var outboxCount = await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "OutboxEvents" WHERE "EventType" = 'OrderPlaced' AND "ProcessedAt" IS NULL""");
        Assert.True(outboxCount >= 1);
    }

    [Fact]
    public async Task CreateOrder_second_call_same_idempotency_returns_same_order()
    {
        var idem = $"idem-{Guid.NewGuid():N}";
        var cmd1 = new CreateOrderCommand(
            Guid.NewGuid().ToString(),
            "t1",
            "s1",
            "cust-1",
            idem,
            [
                new CreateOrderLine(
                    "line-a",
                    "p1",
                    "v1",
                    "wh-a",
                    1,
                    new OrderDomain.Money(1m, "USD"),
                    "A",
                    "{}"),
            ],
            new OrderDomain.ShippingAddress("1", null, "C", "R", "1", "VN"),
            DateTimeOffset.UtcNow);

        var first = (await _orderService!.CreateOrderAsync(cmd1)).Order;

        var cmd2 = new CreateOrderCommand(
            Guid.NewGuid().ToString(),
            "t1",
            "s1",
            "cust-1",
            idem,
            [
                new CreateOrderLine(
                    "line-b",
                    "p2",
                    "v2",
                    "wh-b",
                    99,
                    new OrderDomain.Money(99m, "USD"),
                    "IGNORED",
                    "{}"),
            ],
            new OrderDomain.ShippingAddress("9", null, "X", "Y", "9", "XX"),
            DateTimeOffset.UtcNow);

        var second = (await _orderService.CreateOrderAsync(cmd2)).Order;
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("A", second.Items[0].ProductNameSnapshot);
    }

    private sealed class FakePaymentClient : IPaymentClient
    {
        public Task<PaymentIntentResult> CreatePaymentIntentAsync(
            CreatePaymentIntentRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentIntentResult(
                "pi_test_intent",
                $"https://pay.test/{request.OrderId}?pi=pi_test"));
    }

    private sealed class NoOpInventoryClient : IInventoryClient
    {
        public Task EnsureStockAvailableAsync(
            string tenantId,
            string storeId,
            IReadOnlyList<InventoryCheckLine> lines,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
