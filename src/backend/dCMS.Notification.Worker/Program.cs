using dCMS.Infrastructure.Messaging;
using dCMS.Notification.Api.Rendering;
using dCMS.Notification.Api.Routes;
using dCMS.Notification.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

_ = builder.Configuration.GetConnectionString("Notification")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Notification (used for ProcessedMessages idempotency).");

// Phase C: ProcessedMessages now lives in dcms_notification (no longer cross-DB into dcms_catalog).
builder.Services.AddPostgresConsumedMessageIdempotency(builder.Configuration, "Notification");
builder.Services.AddProcessedMessagesCleanup(builder.Configuration, "Notification");
builder.Services.AddSingleton<TemplateRepository>();
builder.Services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
builder.Services.AddSingleton<NotificationEventsRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddDcmsPublishEnvelopeObserver();
    x.AddDcmsConsumerEndpointDefaults();

    x.AddConsumer<EmailQueuedConsumer>();
    x.AddConsumer<UserNotificationCreatedConsumer>();

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

