using dCMS.Core.Messaging;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Payment.Core;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using dCMS.Payment.Infrastructure.Integration;
using dCMS.Payment.Infrastructure.Messaging;
using dCMS.Payment.Infrastructure.Migrations;
using dCMS.Payment.Infrastructure.Persistence;
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

/// <summary>
/// DAI-342 — end-to-end Order saga ↔ Payment consumer over RabbitMQ + PostgreSQL (Order + Payment schemas in one DB).
/// Ticket mentions SQL Server; this stack uses PostgreSQL for both services (aligned with production compose).
/// </summary>
public sealed class OrderPaymentFlowRabbitMqIntegrationTests : IAsyncLifetime
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
    public async Task Happy_path_ProcessPaymentV1_via_payment_consumer_reaches_Confirmed()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Order"] = cs,
                ["ConnectionStrings:Payment"] = cs,
            }).Build();

        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);
        PaymentDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateBusProvider(cs, host, (ushort)port);
        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var correlationId = orderId;
        var at = DateTimeOffset.UtcNow;
        var tenantStr = tenantId.ToString("D");
        var storeStr = storeId.ToString("D");

        var paymentRepo = provider.GetRequiredService<IPaymentTransactionRepository>();
        await paymentRepo.InsertInitiatedAsync(
            new PaymentTransactionInsert(
                Guid.NewGuid(),
                orderId,
                tenantId,
                storeId,
                "cust-1",
                "card",
                "pi_integration_ok",
                42.5m,
                "USD",
                "stub"),
            CancellationToken.None);

        var bus = provider.GetRequiredService<IBus>();
        try
        {
            await bus.Publish(
                new OrderPlacedV1(
                    orderId.ToString("D"),
                    tenantStr,
                    storeStr,
                    "cust-1",
                    42.5m,
                    "USD",
                    [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    at));

            Assert.Equal(
                "Placed",
                await PollSagaStateAsync(cs, correlationId, "Placed", TimeSpan.FromSeconds(30)));

            await bus.Publish(new StockReservedV1(orderId.ToString("D"), tenantStr, storeStr, at));

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

    [Fact]
    public async Task Failure_path_gateway_decline_moves_saga_to_Cancelled()
    {
        var cs = _postgres!.GetConnectionString();
        var host = _rabbit!.Hostname;
        var port = _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Order"] = cs,
                ["ConnectionStrings:Payment"] = cs,
            }).Build();

        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);
        PaymentDatabaseUpgrader.Run(config, NullLogger.Instance);

        await using var provider = CreateBusProvider(cs, host, (ushort)port);
        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var correlationId = orderId;
        var at = DateTimeOffset.UtcNow;
        var tenantStr = tenantId.ToString("D");
        var storeStr = storeId.ToString("D");

        var paymentRepo = provider.GetRequiredService<IPaymentTransactionRepository>();
        await paymentRepo.InsertInitiatedAsync(
            new PaymentTransactionInsert(
                Guid.NewGuid(),
                orderId,
                tenantId,
                storeId,
                "cust-1",
                "card",
                "pi_decline_integration",
                99m,
                "USD",
                "stub"),
            CancellationToken.None);

        var bus = provider.GetRequiredService<IBus>();
        try
        {
            await bus.Publish(
                new OrderPlacedV1(
                    orderId.ToString("D"),
                    tenantStr,
                    storeStr,
                    "cust-1",
                    99m,
                    "USD",
                    [new OrderPlacedLineV1("var-1", "wh-1", 1)],
                    at));

            Assert.Equal(
                "Placed",
                await PollSagaStateAsync(cs, correlationId, "Placed", TimeSpan.FromSeconds(30)));

            await bus.Publish(new StockReservedV1(orderId.ToString("D"), tenantStr, storeStr, at));

            Assert.Equal(
                "Cancelled",
                await PollSagaStateAsync(cs, correlationId, "Cancelled", TimeSpan.FromSeconds(45)));
        }
        finally
        {
            foreach (var hosted in provider.GetServices<IHostedService>())
                await hosted.StopAsync(CancellationToken.None);
        }
    }

    private static ServiceProvider CreateBusProvider(string orderAndPaymentCs, string rabbitHost, ushort rabbitPort)
    {
        var busConfig = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Order"] = orderAndPaymentCs,
                ["ConnectionStrings:Payment"] = orderAndPaymentCs,
                ["RabbitMq:Host"] = rabbitHost,
                ["RabbitMq:Port"] = rabbitPort.ToString(),
                ["RabbitMq:User"] = RabbitMqBuilder.DefaultUsername,
                ["RabbitMq:Pass"] = RabbitMqBuilder.DefaultPassword,
            }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(busConfig);
        services.AddLogging();
        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        services.AddSingleton<IPaymentTransactionRepository, PostgresPaymentTransactionRepository>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<ProcessPaymentConsumer>();
            bus.AddSagaStateMachine<OrderSaga, OrderSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                    r.AddDbContext<DbContext, OrderSagaDbContext>((_, builder) =>
                        builder.UseNpgsql(orderAndPaymentCs));
                    r.UsePostgres();
                });

            bus.SetKebabCaseEndpointNameFormatter();
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

        return services.BuildServiceProvider(true);
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
