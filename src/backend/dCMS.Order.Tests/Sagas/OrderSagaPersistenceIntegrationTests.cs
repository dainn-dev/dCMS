using dCMS.Core.Messaging;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Messaging;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace dCMS.Order.Tests.Sagas;

/// <summary>
/// DAI-319 — Order saga instances persist to PostgreSQL via MassTransit.EntityFrameworkCore (pessimistic + <c>UsePostgres</c>).
/// DAI-320 — Full happy-path integration: bus + EF saga repository + Postgres assert terminal <c>Confirmed</c>.
/// DAI-322 (US-20) — Compensation: <see cref="PaymentFailedV1"/> → <see cref="ReleaseStockV1"/> + saga <c>Cancelled</c> in Postgres.
/// DAI-323 / DAI-352 (US-F2) — <see cref="StockReservationTimeoutV1"/>: saga <c>Cancelled</c>, no <see cref="ReleaseStockV1"/>, outbox reason <c>stock_reservation_timeout</c>.
/// DAI-351 (US-F2) — <see cref="StockReservationFailedV1"/>: cancelled without <see cref="ReleaseStockV1"/>.
/// DAI-321 (US-20) — <see cref="OrderCancelledV1"/> + <see cref="OrderCancelledIntegrationConsumer"/>: read model <c>Cancelled</c> + outbox; payment timeout persistence.
/// </summary>
public sealed class OrderSagaPersistenceIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    private static void ConfigureEfRepository(string connectionString, IBusRegistrationConfigurator bus)
    {
        bus.AddSagaStateMachine<OrderSaga, OrderSagaState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                r.AddDbContext<DbContext, OrderSagaDbContext>((_, builder) =>
                    builder.UseNpgsql(connectionString));
                r.UsePostgres();
            });
        bus.AddConsumer<OrderCancelledIntegrationConsumer>();
    }

    private static IServiceCollection CreateHarnessServiceCollection(string cs, IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IOrderDetailCache, NullOrderDetailCache>();
        services.AddMassTransitTestHarness(bus => ConfigureEfRepository(cs, bus));
        return services;
    }

    private static async Task InsertPaymentPendingOrderAsync(string cs, Guid orderId, string idempotencyKey)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO "Orders" (
                "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "SubTotal", "TaxTotal", "Total",
                "PaymentIntentId", "IdempotencyKey", "CreatedAt", "UpdatedAt", "ShippingAddress")
            VALUES (
                @Id, 'tenant-1', 'store-1', 'cust-1', 'PaymentPending', 'USD', 42.5, 0, 42.5,
                NULL, @Idem, NOW(), NOW(), '{}'::jsonb)
            """,
            conn);
        cmd.Parameters.AddWithValue("id", orderId);
        cmd.Parameters.AddWithValue("idem", idempotencyKey);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> GetOrderStatusAsync(string cs, Guid orderId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT "Status" FROM "Orders" WHERE "Id" = @id""",
            conn);
        cmd.Parameters.AddWithValue("id", orderId);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private static async Task<int> CountOutboxOrderCancelledForOrderAsync(string cs, string orderId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT COUNT(*)::int
            FROM "OutboxEvents"
            WHERE "EventType" = 'OrderCancelled' AND "Payload"::jsonb->>'orderId' = @oid
            """,
            conn);
        cmd.Parameters.AddWithValue("oid", orderId);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    /// <summary>MassTransit test harness can report inactivity before the separate consumer endpoint finishes (DAI-321).</summary>
    private static async Task AssertEventuallyOrderStatusAsync(string cs, Guid orderId, string expectedStatus, int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await GetOrderStatusAsync(cs, orderId) == expectedStatus)
                return;
            await Task.Delay(25);
        }

        Assert.Equal(expectedStatus, await GetOrderStatusAsync(cs, orderId));
    }

    [Fact]
    public async Task OrderPlaced_persists_saga_row_to_OrderSagaState_table()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateHarnessServiceCollection(cs, config).BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: DateTimeOffset.UtcNow));
            await harness.InactivityTask;

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """
                SELECT "CurrentState", "OrderId", "TenantId"
                FROM "OrderSagaState"
                WHERE "CorrelationId" = @id
                """,
                conn);
            cmd.Parameters.AddWithValue("id", correlationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Expected saga row in OrderSagaState.");
            Assert.Equal("Placed", reader.GetString(0));
            Assert.Equal(orderId, reader.GetString(1));
            Assert.Equal("tenant-1", reader.GetString(2));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Full_happy_path_persists_Confirmed_in_OrderSagaState_table()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateHarnessServiceCollection(cs, config).BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.PaymentPending, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new PaymentCompletedV1(orderId, "pay-1", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Confirmed, TimeSpan.FromSeconds(5)));

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """
                SELECT "CurrentState"
                FROM "OrderSagaState"
                WHERE "CorrelationId" = @id
                """,
                conn);
            cmd.Parameters.AddWithValue("id", correlationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Expected saga row in OrderSagaState.");
            Assert.Equal("Confirmed", reader.GetString(0));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Payment_failed_compensation_persists_Cancelled_and_sends_ReleaseStock()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateHarnessServiceCollection(cs, config).BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await InsertPaymentPendingOrderAsync(cs, correlationId, orderId);

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.PaymentPending, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new PaymentFailedV1(orderId, "payment_failed", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            var released =
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>();
            Assert.True(released, "Expected saga to emit ReleaseStockV1 after PaymentFailed.");
            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>(),
                "Expected OrderCancelledV1 for Notification/Analytics and read-model consumer (DAI-321).");

            await AssertEventuallyOrderStatusAsync(cs, correlationId, "Cancelled");
            Assert.True(
                await CountOutboxOrderCancelledForOrderAsync(cs, orderId) >= 1,
                "Expected OrderCancelled outbox row after consumer.");

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var reasonCmd = new NpgsqlCommand(
                """
                SELECT "Payload"::jsonb->>'reason' AS r
                FROM "OutboxEvents"
                WHERE "EventType" = 'OrderCancelled' AND "Payload"::jsonb->>'orderId' = @oid
                ORDER BY "Id" DESC
                LIMIT 1
                """,
                conn);
            reasonCmd.Parameters.AddWithValue("oid", orderId);
            Assert.Equal("payment_failed", (string?)await reasonCmd.ExecuteScalarAsync());

            await using var cmd = new NpgsqlCommand(
                """
                SELECT "CurrentState"
                FROM "OrderSagaState"
                WHERE "CorrelationId" = @id
                """,
                conn);
            cmd.Parameters.AddWithValue("id", correlationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Expected saga row in OrderSagaState.");
            Assert.Equal("Cancelled", reader.GetString(0));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Stock_reservation_failed_persists_Cancelled_without_ReleaseStock_and_outbox_stock_unavailable()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateHarnessServiceCollection(cs, config).BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await InsertPaymentPendingOrderAsync(cs, correlationId, orderId);

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            await harness.InactivityTask;

            await harness.Bus.Publish(
                new StockReservationFailedV1(orderId, "stock_unavailable", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            Assert.False(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>(),
                "Stock was never reserved; saga must not emit ReleaseStockV1 (DAI-351).");
            Assert.False(await harness.Sent.Any<ProcessPaymentV1>() || await harness.Published.Any<ProcessPaymentV1>());
            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());

            await AssertEventuallyOrderStatusAsync(cs, correlationId, "Cancelled");
            Assert.True(await CountOutboxOrderCancelledForOrderAsync(cs, orderId) >= 1);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var reasonCmd = new NpgsqlCommand(
                """
                SELECT "Payload"::jsonb->>'reason' AS r
                FROM "OutboxEvents"
                WHERE "EventType" = 'OrderCancelled' AND "Payload"::jsonb->>'orderId' = @oid
                ORDER BY "Id" DESC
                LIMIT 1
                """,
                conn);
            reasonCmd.Parameters.AddWithValue("oid", orderId);
            Assert.Equal("stock_unavailable", (string?)await reasonCmd.ExecuteScalarAsync());

            await using var cmd = new NpgsqlCommand(
                """
                SELECT "CurrentState"
                FROM "OrderSagaState"
                WHERE "CorrelationId" = @id
                """,
                conn);
            cmd.Parameters.AddWithValue("id", correlationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Expected saga row in OrderSagaState.");
            Assert.Equal("Cancelled", reader.GetString(0));
        }
        finally
        {
            await harness.Stop();
        }
    }

    /// <summary>
    /// DAI-352: timeout thực tế do hạ tầng (Inventory) publish <see cref="StockReservationTimeoutV1"/>; saga không schedule nội bộ 30s.
    /// Virtual clock +31s with MassTransit Schedule would be a follow-up if timeouts move into the state machine.
    /// </summary>
    [Fact]
    public async Task Stock_reservation_timeout_persists_Cancelled_without_ReleaseStock()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateHarnessServiceCollection(cs, config).BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await InsertPaymentPendingOrderAsync(cs, correlationId, orderId);

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new StockReservationTimeoutV1(orderId));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            Assert.False(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>(),
                "Stock was never reserved; saga must not emit ReleaseStockV1 (DAI-323).");
            Assert.False(await harness.Sent.Any<ProcessPaymentV1>() || await harness.Published.Any<ProcessPaymentV1>());
            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());

            await AssertEventuallyOrderStatusAsync(cs, correlationId, "Cancelled");
            Assert.True(await CountOutboxOrderCancelledForOrderAsync(cs, orderId) >= 1);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var reasonCmd = new NpgsqlCommand(
                """
                SELECT "Payload"::jsonb->>'reason' AS r
                FROM "OutboxEvents"
                WHERE "EventType" = 'OrderCancelled' AND "Payload"::jsonb->>'orderId' = @oid
                ORDER BY "Id" DESC
                LIMIT 1
                """,
                conn);
            reasonCmd.Parameters.AddWithValue("oid", orderId);
            Assert.Equal("stock_reservation_timeout", (string?)await reasonCmd.ExecuteScalarAsync());

            await using var cmd = new NpgsqlCommand(
                """
                SELECT "CurrentState"
                FROM "OrderSagaState"
                WHERE "CorrelationId" = @id
                """,
                conn);
            cmd.Parameters.AddWithValue("id", correlationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Expected saga row in OrderSagaState.");
            Assert.Equal("Cancelled", reader.GetString(0));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Payment_timeout_compensation_persists_Cancelled_and_sends_ReleaseStock()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateHarnessServiceCollection(cs, config).BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await InsertPaymentPendingOrderAsync(cs, correlationId, orderId);

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.PaymentPending, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new PaymentTimeoutV1(orderId));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            var released =
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>();
            Assert.True(released, "Expected saga to emit ReleaseStockV1 after PaymentTimeout.");
            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());

            await AssertEventuallyOrderStatusAsync(cs, correlationId, "Cancelled");
            Assert.True(await CountOutboxOrderCancelledForOrderAsync(cs, orderId) >= 1);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var reasonCmd = new NpgsqlCommand(
                """
                SELECT "Payload"::jsonb->>'reason' AS r
                FROM "OutboxEvents"
                WHERE "EventType" = 'OrderCancelled' AND "Payload"::jsonb->>'orderId' = @oid
                ORDER BY "Id" DESC
                LIMIT 1
                """,
                conn);
            reasonCmd.Parameters.AddWithValue("oid", orderId);
            var reason = (string?)await reasonCmd.ExecuteScalarAsync();
            Assert.Equal("payment_timeout", reason);

            await using var cmd = new NpgsqlCommand(
                """
                SELECT "CurrentState"
                FROM "OrderSagaState"
                WHERE "CorrelationId" = @id
                """,
                conn);
            cmd.Parameters.AddWithValue("id", correlationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Expected saga row in OrderSagaState.");
            Assert.Equal("Cancelled", reader.GetString(0));
        }
        finally
        {
            await harness.Stop();
        }
    }
}
