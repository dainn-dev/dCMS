using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Integration;
using dCMS.Order.Infrastructure.Messaging;
using dCMS.Order.Infrastructure.Operations;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using dCMS.Order.Infrastructure.Services;
using dCMS.Order.Infrastructure.Shipping;
using dCMS.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;

namespace dCMS.Order.Infrastructure;

public static class OrderServiceCollectionExtensions
{
    public static IServiceCollection AddOrderInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInventoryHttpClient(configuration);
        services.AddPaymentHttpClient(configuration);
        services.AddHostedService<OrderDbMigrationHostedService>();
        services.AddSingleton<OrderQueryStore>();
        services.AddSingleton<ShipmentQueryStore>();
        services.AddSingleton<ShipmentPollingStore>();
        services.AddSingleton<ICarrierStatusMapper, ConfigCarrierStatusMapper>();
        services.AddSingleton<ShipmentWebhookProcessor>();
        services.AddHttpClient(nameof(HttpCarrierTrackingClient))
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));
        services.AddSingleton<ICarrierTrackingClient, HttpCarrierTrackingClient>();
        services.AddHostedService<ShipmentPollingWorker>();
        services.AddSingleton<IOrderDetailCache>(sp =>
        {
            var mux = sp.GetService<IConnectionMultiplexer>();
            return mux is null ? new NullOrderDetailCache() : new RedisOrderDetailCache(mux);
        });
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IOrderDlqAdminRepository, OrderDlqAdminRepository>();
        services.AddSingleton<IOrderFailureRepository, PgOrderFailureRepository>();
        services.AddHttpClient(nameof(OperationAlerts));
        services.AddSingleton<IOperationAlerts, OperationAlerts>();
        services.AddHostedService<OrderOutboxRelayHostedService>();
        services.AddPostgresConsumedMessageIdempotency(configuration, "Order");
        services.AddProcessedMessagesCleanup(configuration, "Order");
        services.AddMassTransit(bus =>
        {
            bus.AddDcmsPublishEnvelopeObserver();
            bus.AddDcmsConsumerEndpointDefaults();
            bus.AddConsumer<OrderPaymentSettledConsumer>();
            bus.AddConsumer<OrderCancelledIntegrationConsumer>();
            bus.AddConsumer<OrderStatusProjectionConsumer>();
            bus.AddConsumer<OrderFailureConsumer>();
            bus.AddSagaStateMachine<OrderSaga, OrderSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                    r.AddDbContext<DbContext, OrderSagaDbContext>((_, builder) =>
                    {
                        var cs = configuration.GetConnectionString("Order")
                            ?? throw new InvalidOperationException(
                                "ConnectionStrings:Order is required for MassTransit saga persistence (DAI-319).");
                        builder.UseNpgsql(cs);
                    });
                    r.UsePostgres();
                });
            bus.SetKebabCaseEndpointNameFormatter();
            bus.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var user = configuration["RabbitMq:User"] ?? "guest";
                var pass = configuration["RabbitMq:Pass"] ?? "guest";
                if (ushort.TryParse(configuration["RabbitMq:Port"], out var port) && port > 0)
                {
                    cfg.Host(host, port, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });
                }
                else
                {
                    cfg.Host(host, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });
                }

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>DAI-315 — Typed HttpClient to Payment with Polly (2 retries, 200 ms backoff) on transient HTTP failures.</summary>
    public static IServiceCollection AddPaymentHttpClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Payment:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Payment:BaseUrl is required (e.g. http://localhost:5004/ or http://payment-api:8080/).");
        var apiKey = configuration["Payment:InternalApiKey"];

        services.AddHttpClient<IPaymentClient, HttpPaymentClient>(client =>
            {
                var normalized = baseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(normalized, UriKind.Absolute);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

        return services;
    }

    /// <summary>DAI-314 — Typed HttpClient to Inventory with Polly (2 retries, 200 ms backoff) on transient HTTP failures.</summary>
    public static IServiceCollection AddInventoryHttpClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Inventory:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Inventory:BaseUrl is required (e.g. http://localhost:5002/ or http://inventory-api:8080/).");
        var apiKey = configuration["Inventory:InternalApiKey"];

        services.AddHttpClient<IInventoryClient, HttpInventoryClient>(client =>
            {
                var normalized = baseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(normalized, UriKind.Absolute);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

        return services;
    }
}
