using dCMS.Infrastructure.Messaging;
using dCMS.Infrastructure.Monitoring;
using dCMS.Reports.Worker;
using dCMS.Reports.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

_ = builder.Configuration.GetConnectionString("Analytics")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Analytics.");

// Phase C / P0 #2: Reports.Worker no longer touches dcms_order or dcms_catalog directly.
// OrderPlacedV1 carries item snapshots (via OrderPlacedItemV1); category lookups go through
// Catalog.Api /internal/catalog/.../category endpoint.
builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "reports-worker");

builder.Services.AddHostedService<AnalyticsDbMigrationHostedService>();

// AddDcmsConsumerEndpointDefaults wires the shared MessageIdempotencyConsumeFilter onto every
// consumer endpoint, which needs IIdempotencyService — register it (backed by the analytics
// "ProcessedMessages" table) or every consume faults with "Unable to resolve IIdempotencyService".
builder.Services.AddPostgresConsumedMessageIdempotency(builder.Configuration, "Analytics");
builder.Services.AddProcessedMessagesCleanup(builder.Configuration, "Analytics");

builder.Services.Configure<CatalogClientOptions>(builder.Configuration.GetSection(CatalogClientOptions.SectionName));
builder.Services.AddHttpClient(CatalogClientOptions.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));

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

