using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Core.Cart;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Cart;
using dCMS.Order.Infrastructure.Integration;
using dCMS.Order.Infrastructure.Messaging;
using dCMS.Order.Infrastructure.Operations;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using dCMS.Order.Infrastructure.Services;
using dCMS.Order.Infrastructure.Shipping;
using dCMS.Infrastructure.Catalog;
using dCMS.Infrastructure.Messaging;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
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
        services.AddPromotionsHttpClient(configuration);
        services.AddTenderHttpClients(configuration);
        services.AddSingleton<IPaymentComponentDispatchLog, SqlPaymentComponentDispatchLog>();
        services.AddHostedService<OrderDbMigrationHostedService>();
        services.AddSingleton<OrderQueryStore>();
        services.AddSingleton<OrderReportQueryStore>();
        services.AddSingleton<PaymentTransactionQueryStore>();
        services.AddSingleton<OrderPaymentQueryStore>();
        services.AddSingleton<OrderPaymentRepository>();
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
        services.AddSingleton<ICartStore>(sp =>
        {
            var mux = sp.GetService<IConnectionMultiplexer>();
            return mux is null ? new InMemoryCartStore() : new RedisCartStore(mux);
        });
        services.AddSingleton<IOrderService, OrderService>();

        var catalogCs = configuration.GetConnectionString("Catalog");
        if (!string.IsNullOrWhiteSpace(catalogCs))
        {
            services.AddSingleton<IStoreQuantityLimitPersistence>(_ => new SqlStoreQuantityLimitPersistence(catalogCs));
            services.AddSingleton<ICatalogPersistence>(_ => new SqlCatalogPersistence(catalogCs));
        }

        var orderCs = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        services.AddSingleton<ICustomerOrderQuantityQuery>(_ => new SqlCustomerOrderQuantityQuery(orderCs));
        services.AddSingleton(sp => new QuantityLimitValidationService(
            sp.GetRequiredService<IStoreQuantityLimitPersistence>(),
            sp.GetRequiredService<ICatalogPersistence>(),
            sp.GetRequiredService<ICustomerOrderQuantityQuery>()));

        services.AddSingleton<IOrderDlqAdminRepository, OrderDlqAdminRepository>();
        services.AddSingleton<IOrderFailureRepository, PgOrderFailureRepository>();
        services.AddSingleton<IReturnsRepository, PgReturnsRepository>();
        services.AddSingleton(sp =>
        {
            var cs = configuration.GetConnectionString("Order")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Order is required for OrderPromotionSnapshotReader (DAI-693).");
            return new OrderPromotionSnapshotReader(cs);
        });
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
            bus.AddConsumer<OrderRedemptionConfirmConsumer>();
            bus.AddConsumer<OrderRedemptionReleaseConsumer>();
            // DAI-724: multi-tender payment orchestration consumers.
            bus.AddConsumer<PaymentOrchestrator>();
            bus.AddConsumer<ReleasePaymentComponentsConsumer>();
            bus.AddConsumer<CommerceNotificationPublisher>();
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

    /// <summary>DAI-693 — Typed HttpClient to Promotions with Polly (2 retries, 200 ms backoff) on transient HTTP failures.</summary>
    public static IServiceCollection AddPromotionsHttpClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Promotions:BaseUrl"] ?? "http://promotions-api:8080/";

        services.AddHttpClient<IPromotionsClient, HttpPromotionsClient>(client =>
            {
                var normalized = baseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(normalized, UriKind.Absolute);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

        return services;
    }

    /// <summary>
    /// DAI-724 — Typed HTTP clients for the multi-tender payment orchestrator. Voucher/Loyalty
    /// services reach the gateway (or local dev ports) with the same Polly transient-error policy
    /// (2 retries, 200 ms backoff) used elsewhere in this project.
    /// </summary>
    public static IServiceCollection AddTenderHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var voucherBase = configuration["Voucher:BaseUrl"] ?? "http://voucher-api:8080/";
        var loyaltyBase = configuration["Loyalty:BaseUrl"] ?? "http://loyalty-api:8080/";
        var paymentBase = configuration["Payment:BaseUrl"] ?? "http://payment-api:8080/";

        services.AddHttpClient<IVoucherTenderClient, HttpVoucherTenderClient>(client =>
            {
                client.BaseAddress = new Uri(voucherBase.TrimEnd('/') + "/", UriKind.Absolute);
            })
            .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

        services.AddHttpClient<ILoyaltyTenderClient, HttpLoyaltyTenderClient>(client =>
            {
                client.BaseAddress = new Uri(loyaltyBase.TrimEnd('/') + "/", UriKind.Absolute);
            })
            .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

        // DAI-689: gateway client. Default to stub (matches dCMS.Payment.Infrastructure default).
        if (configuration.GetValue("Payment:UseStubGateway", true))
        {
            services.AddSingleton<IGatewayTenderClient, StubGatewayTenderClient>();
        }
        else
        {
            services.AddHttpClient<IGatewayTenderClient, HttpGatewayTenderClient>(client =>
                {
                    client.BaseAddress = new Uri(paymentBase.TrimEnd('/') + "/", UriKind.Absolute);
                })
                .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));
        }

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
