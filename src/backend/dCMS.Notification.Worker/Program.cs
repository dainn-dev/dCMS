using dCMS.Infrastructure.Messaging;
using dCMS.Notification.Api.Rendering;
using dCMS.Notification.Api.Routes;
using dCMS.Notification.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

_ = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Catalog.");

builder.Services.AddPostgresConsumedMessageIdempotency(builder.Configuration, "Catalog");
builder.Services.AddProcessedMessagesCleanup(builder.Configuration, "Catalog");
builder.Services.AddSingleton<TemplateRepository>();
builder.Services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();

builder.Services.AddMassTransit(x =>
{
    x.AddDcmsPublishEnvelopeObserver();
    x.AddDcmsConsumerEndpointDefaults();

    x.AddConsumer<EmailQueuedConsumer>();

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

