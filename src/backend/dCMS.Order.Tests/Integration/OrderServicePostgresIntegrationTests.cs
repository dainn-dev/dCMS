using Dapper;
using dCMS.Core.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using OrderDomain = dCMS.Order.Core.Domain;
using dCMS.Order.Infrastructure.Caching;
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
    private readonly Mock<IBus> _busMock = new();

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
                ["ConnectionStrings:Payment"] = connectionString,
            })
            .Build();

        OrderDatabaseUpgrader.Run(configuration, NullLogger.Instance);

        _busMock
            .Setup(b => b.Publish(It.IsAny<OrderCustomerCancellationV1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IInventoryClient, NoOpInventoryClient>();
        services.AddSingleton<IPaymentClient, StubPaymentClient>();
        services.AddSingleton<OrderQueryStore>();
        services.AddSingleton<PaymentTransactionQueryStore>();
        services.AddSingleton<IOrderDetailCache, NullOrderDetailCache>();
        services.AddSingleton(_busMock.Object);
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
        Assert.Equal(StubPaymentClient.CheckoutUrl(orderId), created.PaymentUrl);
        Assert.Equal(OrderDomain.OrderStatus.PaymentPending, created.Order.Status);
        Assert.Equal($"pi_stub_{orderId}", created.Order.PaymentIntentId);

        var loaded = await _orderService.GetByIdAsync("t1", "s1", orderId);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Items);
        Assert.Equal("Widget", loaded.Items[0].ProductNameSnapshot);
        Assert.Equal($"pi_stub_{orderId}", loaded.PaymentIntentId);

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

    [Fact]
    public async Task GetTimedById_returns_order_with_created_at()
    {
        var orderId = Guid.NewGuid().ToString();
        var cmd = new CreateOrderCommand(
            orderId,
            "t1",
            "s1",
            "cust-list",
            $"idem-{Guid.NewGuid():N}",
            [
                new CreateOrderLine(
                    "line-1",
                    "p1",
                    "v1",
                    "wh-1",
                    1,
                    new OrderDomain.Money(5m, "USD"),
                    "P",
                    "{}"),
            ],
            new OrderDomain.ShippingAddress("1 A", null, "C", "R", "1", "VN"),
            DateTimeOffset.Parse("2026-04-13T10:00:00Z"));

        await _orderService!.CreateOrderAsync(cmd);

        var timed = await _orderService.GetTimedByIdAsync("t1", "s1", orderId);
        Assert.NotNull(timed);
        Assert.Equal(orderId, timed!.Order.Id);
        Assert.Equal("cust-list", timed.Order.CustomerId);
        Assert.True(timed.CreatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ListOrders_filters_by_customer_and_cursor_pages()
    {
        var idem1 = $"idem-{Guid.NewGuid():N}";
        var idem2 = $"idem-{Guid.NewGuid():N}";
        var o1 = Guid.NewGuid().ToString();
        var o2 = Guid.NewGuid().ToString();

        await _orderService!.CreateOrderAsync(new CreateOrderCommand(
            o1, "t1", "s1", "cust-a", idem1,
            [new CreateOrderLine("l1", "p", "v", "wh", 1, new OrderDomain.Money(1m, "USD"), "N", "{}")],
            new OrderDomain.ShippingAddress("1", null, "C", "R", "1", "VN"),
            DateTimeOffset.Parse("2026-04-13T12:00:01Z")));

        await _orderService.CreateOrderAsync(new CreateOrderCommand(
            o2, "t1", "s1", "cust-a", idem2,
            [new CreateOrderLine("l2", "p", "v", "wh", 1, new OrderDomain.Money(2m, "USD"), "N", "{}")],
            new OrderDomain.ShippingAddress("2", null, "C", "R", "2", "VN"),
            DateTimeOffset.Parse("2026-04-13T12:00:02Z")));

        var page1 = await _orderService.ListOrdersAsync(
            new OrderListQuery("t1", "s1", "cust-a", null, null, 1));
        Assert.Single(page1.Items);
        Assert.NotNull(page1.NextCursor);

        var page2 = await _orderService.ListOrdersAsync(
            new OrderListQuery("t1", "s1", "cust-a", null, page1.NextCursor, 1));
        Assert.Single(page2.Items);
        Assert.NotEqual(page1.Items[0].Order.Id, page2.Items[0].Order.Id);
        var both = new[] { page1.Items[0].Order.Id, page2.Items[0].Order.Id }.ToHashSet();
        Assert.Contains(o1, both);
        Assert.Contains(o2, both);

        var filtered = await _orderService.ListOrdersAsync(
            new OrderListQuery("t1", "s1", null, "payment_pending", null, 50));
        var pendingIds = filtered.Items.Select(i => i.Order.Id).ToHashSet();
        Assert.Contains(o1, pendingIds);
        Assert.Contains(o2, pendingIds);
    }

    [Fact]
    public async Task ListOrders_invalid_status_throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService!.ListOrdersAsync(new OrderListQuery("t1", "s1", null, "not-a-status", null, 10)));
    }

    [Fact]
    public async Task ListOrders_multi_status_returns_union_and_deduplicates()
    {
        var o1 = Guid.NewGuid().ToString();
        var o2 = Guid.NewGuid().ToString();

        await _orderService!.CreateOrderAsync(new CreateOrderCommand(
            o1, "t1", "s1", "cust-x", $"idem-{Guid.NewGuid():N}",
            [new CreateOrderLine("l1", "p", "v", "wh", 1, new OrderDomain.Money(1m, "USD"), "N", "{}")],
            new OrderDomain.ShippingAddress("1", null, "C", "R", "1", "VN"),
            DateTimeOffset.UtcNow));

        await _orderService.CreateOrderAsync(new CreateOrderCommand(
            o2, "t1", "s1", "cust-x", $"idem-{Guid.NewGuid():N}",
            [new CreateOrderLine("l2", "p", "v", "wh", 1, new OrderDomain.Money(2m, "USD"), "N", "{}")],
            new OrderDomain.ShippingAddress("2", null, "C", "R", "2", "VN"),
            DateTimeOffset.UtcNow));

        // default status is PaymentPending; query should return both even with duplicates + whitespace
        var page = await _orderService.ListOrdersAsync(
            new OrderListQuery("t1", "s1", null, "payment_pending, payment_pending", null, 50));
        var ids = page.Items.Select(i => i.Order.Id).ToHashSet();
        Assert.Contains(o1, ids);
        Assert.Contains(o2, ids);
    }

    [Fact]
    public async Task ListOrders_multi_status_invalid_part_throws_with_part_name()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService!.ListOrdersAsync(new OrderListQuery("t1", "s1", null, "payment_failed,not-a-status", null, 10)));
        Assert.Contains("not-a-status", ex.Message);
    }

    [Fact]
    public async Task ListOrders_multi_status_more_than_10_throws()
    {
        var many = string.Join(',', Enumerable.Repeat("payment_pending", 11));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService!.ListOrdersAsync(new OrderListQuery("t1", "s1", null, many, null, 10)));
    }

    [Fact]
    public async Task CancelOrder_payment_pending_updates_row_outbox_and_publishes_customer_cancellation()
    {
        _busMock.Invocations.Clear();
        var orderId = Guid.NewGuid().ToString();
        await _orderService!.CreateOrderAsync(new CreateOrderCommand(
            orderId,
            "t1",
            "s1",
            "cust-1",
            $"idem-{Guid.NewGuid():N}",
            [
                new CreateOrderLine(
                    "l1",
                    "p1",
                    "v1",
                    "wh-1",
                    1,
                    new OrderDomain.Money(10m, "USD"),
                    "P",
                    "{}"),
            ],
            new OrderDomain.ShippingAddress("1", null, "C", "R", "1", "VN"),
            DateTimeOffset.UtcNow));

        var result = await _orderService.CancelOrderAsync(
            new CancelOrderCommand("t1", "s1", orderId, $"cancel-{Guid.NewGuid():N}", null, "user changed mind", DateTimeOffset.UtcNow));

        var ok = Assert.IsType<CancelOrderResult.Ok>(result);
        Assert.Equal(OrderDomain.OrderStatus.Cancelled, ok.Order.Status);

        await using var conn = new NpgsqlConnection(_postgres!.GetConnectionString());
        await conn.OpenAsync();
        var status = (string?)await conn.ExecuteScalarAsync(
            """SELECT "Status" FROM "Orders" WHERE "Id" = @Id""",
            new { Id = Guid.Parse(orderId) });
        Assert.Equal(nameof(OrderDomain.OrderStatus.Cancelled), status);

        var outbox = await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "OutboxEvents" WHERE "EventType" = 'OrderCancelled' AND "Payload"::jsonb->>'orderId' = @oid""",
            new { oid = orderId });
        Assert.True(outbox >= 1);

        _busMock.Verify(
            b => b.Publish(It.Is<OrderCustomerCancellationV1>(m => m.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelOrder_wrong_customer_returns_forbidden()
    {
        _busMock.Invocations.Clear();
        var orderId = Guid.NewGuid().ToString();
        await _orderService!.CreateOrderAsync(new CreateOrderCommand(
            orderId,
            "t1",
            "s1",
            "cust-1",
            $"idem-{Guid.NewGuid():N}",
            [
                new CreateOrderLine("l1", "p1", "v1", "wh-1", 1, new OrderDomain.Money(1m, "USD"), "P", "{}"),
            ],
            new OrderDomain.ShippingAddress("1", null, "C", "R", "1", "VN"),
            DateTimeOffset.UtcNow));

        var result = await _orderService.CancelOrderAsync(
            new CancelOrderCommand(
                "t1",
                "s1",
                orderId,
                $"cancel-{Guid.NewGuid():N}",
                CallerCustomerId: "other-user",
                "no",
                DateTimeOffset.UtcNow));

        Assert.IsType<CancelOrderResult.Forbidden>(result);
        _busMock.Verify(
            b => b.Publish(It.IsAny<OrderCustomerCancellationV1>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelOrder_shipped_returns_not_cancellable()
    {
        _busMock.Invocations.Clear();
        var orderId = Guid.NewGuid().ToString();
        await _orderService!.CreateOrderAsync(new CreateOrderCommand(
            orderId,
            "t1",
            "s1",
            "cust-1",
            $"idem-{Guid.NewGuid():N}",
            [
                new CreateOrderLine("l1", "p1", "v1", "wh-1", 1, new OrderDomain.Money(1m, "USD"), "P", "{}"),
            ],
            new OrderDomain.ShippingAddress("1", null, "C", "R", "1", "VN"),
            DateTimeOffset.UtcNow));

        await using (var conn = new NpgsqlConnection(_postgres!.GetConnectionString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(
                """UPDATE "Orders" SET "Status" = 'Shipped' WHERE "Id" = @Id""",
                new { Id = Guid.Parse(orderId) });
        }

        var result = await _orderService.CancelOrderAsync(
            new CancelOrderCommand("t1", "s1", orderId, $"cancel-{Guid.NewGuid():N}", null, "try", DateTimeOffset.UtcNow));

        var nc = Assert.IsType<CancelOrderResult.NotCancellable>(result);
        Assert.Contains("shipped", nc.Message, StringComparison.OrdinalIgnoreCase);
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

    private sealed class StubPaymentClient : IPaymentClient
    {
        public static string CheckoutUrl(string orderId) =>
            $"https://checkout.local/pay/{Uri.EscapeDataString(orderId)}";

        public Task<PaymentIntentResult> CreatePaymentIntentAsync(
            CreatePaymentIntentRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentIntentResult(
                $"pi_stub_{request.OrderId}",
                CheckoutUrl(request.OrderId)));
    }
}
