using dCMS.Infrastructure.Messaging;
using dCMS.Infrastructure.Monitoring;
using dCMS.Reports.Worker;
using dCMS.Reports.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

_ = builder.Configuration.GetConnectionString("Analytics")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Analytics.");
_ = builder.Configuration.GetConnectionString("Order")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Order.");
// Catalog is optional (category rollups will be skipped when missing).

builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "reports-worker");

builder.Services.AddHostedService<AnalyticsDbMigrationHostedService>();

builder.Services.AddMassTransit(x =>
{
    x.AddDcmsPublishEnvelopeObserver();
    x.AddDcmsConsumerEndpointDefaults();

    x.AddConsumer<OrderProjectionConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var r = context.GetRequiredService<IConfiguration>();
        cfg.Host(r["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(r["RabbitMq:User"] ?? "guest");
            h.Password(r["RabbitMq:Pass"] ?? "guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

await builder.Build().RunAsync();

