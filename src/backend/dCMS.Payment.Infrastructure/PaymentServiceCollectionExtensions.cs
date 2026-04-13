using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Integration;
using dCMS.Payment.Infrastructure.Messaging;
using dCMS.Payment.Infrastructure.Persistence;
using dCMS.Payment.Infrastructure.Webhooks;
using dCMS.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dCMS.Payment.Infrastructure;

public static class PaymentServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<PaymentDbMigrationHostedService>();
        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        services.AddSingleton<IPaymentTransactionRepository, PostgresPaymentTransactionRepository>();
        services.AddScoped<CreatePaymentIntentService>();
        services.AddScoped<PaymentGatewayWebhookProcessor>();
        services.AddPostgresConsumedMessageIdempotency(configuration, "Payment");
        services.AddProcessedMessagesCleanup(configuration, "Payment");

        services.AddMassTransit(bus =>
        {
            bus.AddDcmsPublishEnvelopeObserver();
            bus.AddDcmsConsumerEndpointDefaults();
            bus.AddConsumer<ProcessPaymentConsumer>();
            bus.AddConsumer<RefundPaymentConsumer>();
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
}

