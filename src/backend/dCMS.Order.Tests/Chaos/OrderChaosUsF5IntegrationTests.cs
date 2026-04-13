using System.Collections.Concurrent;
using System.Text.Json;
using dCMS.Core.Messaging;
using dCMS.Infrastructure.Messaging;
using dCMS.Infrastructure.Outbox;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using Dapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace dCMS.Order.Tests.Chaos;

/// <summary>
/// US-F5 / DAI-364 subtasks — PostgreSQL + RabbitMQ (ticket SQL Server → repo reality).
/// </summary>
public sealed class OrderChaosUsF5IntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        _rabbit = new RabbitMqBuilder().Build();
        await _postgres.StartAsync();
        await _rabbit.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_rabbit is not null)
            await _rabbit.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    /// <summary>DAI-365 — relay redelivery reuses stable message id; consumer runs once.</summary>
    [Fact]
    public async Task Outbox_relay_first_publish_throws_then_retry_delivers_OrderPlaced_once_to_consumer()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = (ushort)_rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        ChaosOrderPlacedCounterConsumer.Reset();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IOrderDetailCache, NullOrderDetailCache>();
        services.AddLogging();
        services.AddPostgresConsumedMessageIdempotency(config, "Order");
        services.AddMassTransit(bus =>
        {
            bus.AddDcmsConsumerEndpointDefaults();
            bus.AddConsumer<ChaosOrderPlacedCounterConsumer>();
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(host, port, "/", h =>
                {
                    h.Username(RabbitMqBuilder.DefaultUsername);
                    h.Password(RabbitMqBuilder.DefaultPassword);
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        await using var provider = services.BuildServiceProvider(true);
        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var bus = provider.GetRequiredService<IBus>();
        var orderId = Guid.NewGuid().ToString();
        var at = DateTimeOffset.UtcNow;
        await InsertOrderPlacedOutboxRowAsync(
            cs,
            orderId,
            "tenant-1",
            "store-1",
            "cust-1",
            1m,
            "USD",
            at);

        var relay = new SqlOutboxRelay(cs);
        var boom = true;
        await relay.ProcessPendingAsync(
            async (outboxId, m) =>
            {
                await bus.Publish(
                    (OrderPlacedV1)m,
                    ctx => ctx.MessageId = OutboxPublishMessageId.FromOutboxRow(outboxId));
                if (boom)
                {
                    boom = false;
                    throw new InvalidOperationException("simulated crash after publish");
                }
            },
            CancellationToken.None);

        await relay.ProcessPendingAsync(
            async (outboxId, m) =>
            {
                await bus.Publish(
                    (OrderPlacedV1)m,
                    ctx => ctx.MessageId = OutboxPublishMessageId.FromOutboxRow(outboxId));
            },
            CancellationToken.None);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline && ChaosOrderPlacedCounterConsumer.Handled < 1)
            await Task.Delay(200);

        Assert.Equal(1, ChaosOrderPlacedCounterConsumer.Handled);

        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            var processed = await conn.ExecuteScalarAsync<long>(
                """SELECT COUNT(*) FROM "OutboxEvents" WHERE "ProcessedAt" IS NOT NULL""");
            Assert.Equal(1, processed);
        }

        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StopAsync(CancellationToken.None);
    }

    /// <summary>DAI-366 — PaymentCompleted queued while bus stopped; restart consumes → Confirmed.</summary>
    [Fact]
    public async Task PaymentCompleted_published_while_saga_bus_stopped_is_consumed_after_restart()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = (ushort)_rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        var services = BuildSagaServices(cs, host, port);
        await using var provider = services.BuildServiceProvider(true);
        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var busControl = provider.GetRequiredService<IBusControl>();
        var bus = provider.GetRequiredService<IBus>();

        var orderId = Guid.NewGuid().ToString();
        var correlationId = Guid.Parse(orderId);
        var at = DateTimeOffset.UtcNow;

        await bus.Publish(
            new OrderPlacedV1(
                orderId,
                "tenant-1",
                "store-1",
                "cust-1",
                10m,
                "USD",
                [new OrderPlacedLineV1("v1", "w1", 1)],
                at));
        Assert.Equal(
            "Placed",
            await PollSagaStateAsync(cs, correlationId, "Placed", TimeSpan.FromSeconds(30)));

        await bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
        Assert.Equal(
            "PaymentPending",
            await PollSagaStateAsync(cs, correlationId, "PaymentPending", TimeSpan.FromSeconds(30)));

        await busControl.StopAsync(TimeSpan.FromSeconds(15));

        var pub = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(host, port, "/", h =>
            {
                h.Username(RabbitMqBuilder.DefaultUsername);
                h.Password(RabbitMqBuilder.DefaultPassword);
            });
        });
        await pub.StartAsync(CancellationToken.None);
        try
        {
            await pub.Publish(
                new PaymentCompletedV1(orderId, "pay-chaos", "tenant-1", "store-1", at));
        }
        finally
        {
            await pub.StopAsync(CancellationToken.None);
        }

        await busControl.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        try
        {
            Assert.Equal(
                "Confirmed",
                await PollSagaStateAsync(cs, correlationId, "Confirmed", TimeSpan.FromSeconds(45)));
        }
        finally
        {
            foreach (var hosted in provider.GetServices<IHostedService>())
                await hosted.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>DAI-367 — duplicate PaymentCompleted same MessageId; saga reaches Confirmed once.</summary>
    [Fact]
    public async Task Duplicate_PaymentCompleted_same_MessageId_does_not_fault_saga()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = (ushort)_rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        var services = BuildSagaServices(cs, host, port);
        await using var provider = services.BuildServiceProvider(true);
        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var busControl = provider.GetRequiredService<IBusControl>();
        var bus = provider.GetRequiredService<IBus>();

        var orderId = Guid.NewGuid().ToString();
        var correlationId = Guid.Parse(orderId);
        var at = DateTimeOffset.UtcNow;

        await bus.Publish(
            new OrderPlacedV1(
                orderId,
                "tenant-1",
                "store-1",
                "cust-1",
                11m,
                "USD",
                [new OrderPlacedLineV1("v1", "w1", 1)],
                at));
        Assert.Equal("Placed", await PollSagaStateAsync(cs, correlationId, "Placed", TimeSpan.FromSeconds(30)));
        await bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
        Assert.Equal(
            "PaymentPending",
            await PollSagaStateAsync(cs, correlationId, "PaymentPending", TimeSpan.FromSeconds(30)));

        await busControl.StopAsync(TimeSpan.FromSeconds(15));
        var sharedId = NewId.NextGuid();

        var pub = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(host, port, "/", h =>
            {
                h.Username(RabbitMqBuilder.DefaultUsername);
                h.Password(RabbitMqBuilder.DefaultPassword);
            });
        });
        await pub.StartAsync(CancellationToken.None);
        try
        {
            var msg = new PaymentCompletedV1(orderId, "pay-dup", "tenant-1", "store-1", at);
            await pub.Publish(msg, ctx => ctx.MessageId = sharedId);
            await pub.Publish(msg, ctx => ctx.MessageId = sharedId);
        }
        finally
        {
            await pub.StopAsync(CancellationToken.None);
        }

        await busControl.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        try
        {
            Assert.Equal(
                "Confirmed",
                await PollSagaStateAsync(cs, correlationId, "Confirmed", TimeSpan.FromSeconds(45)));
        }
        finally
        {
            foreach (var hosted in provider.GetServices<IHostedService>())
                await hosted.StopAsync(CancellationToken.None);
        }
    }

    private static ServiceCollection BuildSagaServices(string cs, string rabbitHost, ushort rabbitPort)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IOrderDetailCache, NullOrderDetailCache>();
        services.AddLogging();
        services.AddPostgresConsumedMessageIdempotency(config, "Order");
        services.AddMassTransit(bus =>
        {
            bus.AddDcmsConsumerEndpointDefaults();
            bus.AddSagaStateMachine<OrderSaga, OrderSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                    r.AddDbContext<DbContext, OrderSagaDbContext>((_, builder) =>
                        builder.UseNpgsql(cs));
                    r.UsePostgres();
                });
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(RabbitMqBuilder.DefaultUsername);
                    h.Password(RabbitMqBuilder.DefaultPassword);
                });
                cfg.ConfigureEndpoints(context);
            });
        });
        return services;
    }

    private static async Task InsertOrderPlacedOutboxRowAsync(
        string cs,
        string orderId,
        string tenantId,
        string storeId,
        string customerId,
        decimal total,
        string currency,
        DateTimeOffset at)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                orderId,
                tenantId,
                storeId,
                customerId,
                totalAmount = total,
                currency,
                lines = new[] { new { variantId = "v1", warehouseId = "w1", quantity = 1 } },
                occurredAt = at,
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
            VALUES ('OrderPlaced', @p::text, @at, 0)
            """,
            conn);
        cmd.Parameters.AddWithValue("p", payload);
        cmd.Parameters.AddWithValue("at", at);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> PollSagaStateAsync(
        string connectionString,
        Guid correlationId,
        string? waitUntilEquals,
        TimeSpan maxWait)
    {
        var deadline = DateTime.UtcNow + maxWait;
        string? last = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var conn = new NpgsqlConnection(connectionString);
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
            if (await reader.ReadAsync())
            {
                last = reader.GetString(0);
                if (waitUntilEquals is null || last == waitUntilEquals)
                    return last;
            }

            await Task.Delay(300);
        }

        return last;
    }
}

public sealed class ChaosOrderPlacedCounterConsumer : IConsumer<OrderPlacedV1>
{
    private static int _handled;

    public static int Handled => _handled;

    public static void Reset() => _handled = 0;

    public Task Consume(ConsumeContext<OrderPlacedV1> context)
    {
        Interlocked.Increment(ref _handled);
        return Task.CompletedTask;
    }
}
