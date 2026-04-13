using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Integration;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Services;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace dCMS.Order.Infrastructure;

public static class OrderServiceCollectionExtensions
{
    public static IServiceCollection AddOrderInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInventoryHttpClient(configuration);
        services.AddPaymentHttpClient(configuration);
        services.AddHostedService<OrderDbMigrationHostedService>();
        services.AddSingleton<OrderQueryStore>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            bus.UsingRabbitMq((_, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var user = configuration["RabbitMq:User"] ?? "guest";
                var pass = configuration["RabbitMq:Pass"] ?? "guest";
                cfg.Host(host, "/", h =>
                {
                    h.Username(user);
                    h.Password(pass);
                });
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
