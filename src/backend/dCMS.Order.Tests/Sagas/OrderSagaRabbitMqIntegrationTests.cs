using dCMS.Core.Messaging;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace dCMS.Order.Tests.Sagas;

/// <summary>DAI-316 — happy path integration over real RabbitMQ + PostgreSQL saga persistence.</summary>
public sealed class OrderSagaRabbitMqIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
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

    [Fact]
    public async Task Happy_path_over_RabbitMQ_persists_Confirmed_in_OrderSagaState()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMassTransit(bus =>
        {
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
                // Testcontainers.RabbitMq defaults: https://github.com/testcontainers/testcontainers-dotnet
                cfg.Host(host, (ushort)port, "/", h =>
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

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            Assert.Equal(
                "Placed",
                await PollSagaStateAsync(cs, correlationId, "Placed", TimeSpan.FromSeconds(30)));

            await bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            Assert.Equal(
                "PaymentPending",
                await PollSagaStateAsync(cs, correlationId, "PaymentPending", TimeSpan.FromSeconds(30)));

            await bus.Publish(new PaymentCompletedV1(orderId, "pay-1", "tenant-1", "store-1", at));
            Assert.Equal(
                "Confirmed",
                await PollSagaStateAsync(cs, correlationId, "Confirmed", TimeSpan.FromSeconds(30)));
        }
        finally
        {
            foreach (var hosted in provider.GetServices<IHostedService>())
                await hosted.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>DAI-351 (US-F2) — Postgres + RabbitMQ; ticket ghi SQL Server; repo dùng PostgreSQL.</summary>
    [Fact]
    public async Task StockReservationFailed_over_RabbitMQ_persists_Cancelled_in_OrderSagaState()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMassTransit(bus =>
        {
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
                cfg.Host(host, (ushort)port, "/", h =>
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

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    TenantId: "tenant-1",
                    StoreId: "store-1",
                    CustomerId: "cust-1",
                    TotalAmount: 42.5m,
                    Currency: "USD",
                    Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    OccurredAt: at));
            Assert.Equal(
                "Placed",
                await PollSagaStateAsync(cs, correlationId, "Placed", TimeSpan.FromSeconds(30)));

            await bus.Publish(
                new StockReservationFailedV1(orderId, "stock_unavailable", "tenant-1", "store-1", at));
            Assert.Equal(
                "Cancelled",
                await PollSagaStateAsync(cs, correlationId, "Cancelled", TimeSpan.FromSeconds(30)));
        }
        finally
        {
            foreach (var hosted in provider.GetServices<IHostedService>())
                await hosted.StopAsync(CancellationToken.None);
        }
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
