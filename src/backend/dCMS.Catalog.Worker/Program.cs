using dCMS.Catalog.Worker.Consumers;
using dCMS.Catalog.Worker.Imports;
using dCMS.Catalog.Worker.Imports.Processors;
using dCMS.Catalog.Worker.Indexing;
using dCMS.Catalog.Worker.Workers;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Infrastructure;
using dCMS.Infrastructure.Catalog;
using dCMS.Infrastructure.Outbox;
using dCMS.Infrastructure.Search;
using dCMS.Infrastructure.Messaging;
using dCMS.Infrastructure.Monitoring;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var catalogCs = builder.Configuration.GetConnectionString("Catalog")
                ?? throw new InvalidOperationException("Set ConnectionStrings:Catalog.");
var inventoryCs = builder.Configuration.GetConnectionString("Inventory")
                  ?? throw new InvalidOperationException("Set ConnectionStrings:Inventory.");
var esUrl = builder.Configuration["Elasticsearch:Url"]
            ?? throw new InvalidOperationException("Set Elasticsearch:Url.");
var redisCs = builder.Configuration.GetConnectionString("Redis");

builder.Services.Configure<CatalogSearchIndexingOptions>(
    builder.Configuration.GetSection(CatalogSearchIndexingOptions.SectionName));

builder.Services.AddSingleton<ICatalogPersistence>(_ => new SqlCatalogPersistence(catalogCs));
builder.Services.AddSingleton<IProductSearchRepository>(sp =>
{
    var o = sp.GetRequiredService<IOptions<CatalogSearchIndexingOptions>>().Value;
    return new SqlProductSearchRepository(
        sp.GetRequiredService<ICatalogPersistence>(),
        catalogCs,
        inventoryCs,
        o);
});

builder.Services.AddSingleton(_ => new ElasticsearchClientFactory(new Uri(esUrl)));

builder.Services.AddSingleton<ICatalogSearchCacheInvalidator>(_ =>
{
    if (string.IsNullOrWhiteSpace(redisCs))
        return NoopCatalogSearchCacheInvalidator.Instance;
    var mux = StackExchange.Redis.ConnectionMultiplexer.Connect(redisCs);
    return new RedisCatalogSearchCacheInvalidator(mux);
});

builder.Services.AddSingleton<ElasticsearchProductIndexer>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<DebouncedStockProductIndexPublisher>();

builder.Services.AddHttpClient();

// DAI-707 — bulk import infrastructure
builder.Services.Configure<ImportFileReaderOptions>(builder.Configuration.GetSection("Catalog:S3:ImportFiles"));
builder.Services.Configure<CatalogMediaPathOptions>(builder.Configuration.GetSection("Catalog:Media"));
builder.Services.AddSingleton<ImportFileReader>();
builder.Services.AddSingleton<IImportJobPersistence>(_ => new SqlImportJobPersistence(catalogCs));
builder.Services.AddSingleton<IImportRowProcessor>(_ => new ProductRowProcessor(catalogCs));
builder.Services.AddSingleton<IImportRowProcessor>(sp => new ProductImageRowProcessor(
    catalogCs, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<ILogger<ProductImageRowProcessor>>()));
builder.Services.AddSingleton<IImportRowProcessor>(_ => new InventoryRowProcessor(catalogCs, inventoryCs));
builder.Services.AddSingleton<IImportRowProcessor>(_ => new PromoCodeRowProcessor(catalogCs));

builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "catalog-worker");

builder.Services.AddHostedService<CatalogDbMigrationHostedService>();
builder.Services.AddPostgresConsumedMessageIdempotency(builder.Configuration, "Catalog");
builder.Services.AddProcessedMessagesCleanup(builder.Configuration, "Catalog");

builder.Services.AddMassTransit(x =>
{
    x.AddDcmsPublishEnvelopeObserver();
    x.AddDcmsConsumerEndpointDefaults();
    x.AddConsumer<ProductCreatedIndexConsumer>();
    x.AddConsumer<ProductUpdatedIndexConsumer>();
    x.AddConsumer<ProductPublishedIndexConsumer>();
    x.AddConsumer<ProductArchivedIndexConsumer>();
    x.AddConsumer<StockUpdatedIndexConsumer>();
    x.AddConsumer<ImportJobConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var r = context.GetRequiredService<IConfiguration>();
        cfg.Host(r["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(r["RabbitMq:User"] ?? "guest");
            h.Password(r["RabbitMq:Pass"] ?? "guest");
        });

        // DAI-707 (AC6) — per-queue concurrency throttle so bulk imports
        // never starve the rest of the worker pool.
        cfg.ReceiveEndpoint("import-job-queued", e =>
        {
            e.PrefetchCount = 4;
            e.ConcurrentMessageLimit = 4;
            e.UseConcurrencyLimit(4);
            e.ConfigureConsumer<ImportJobConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHostedService(sp => new CatalogOutboxRelayHostedService(
    new SqlOutboxRelay(catalogCs),
    sp.GetRequiredService<IBus>(),
    sp.GetRequiredService<ILogger<CatalogOutboxRelayHostedService>>()));

builder.Services.AddHostedService(sp => new InventoryOutboxRelayHostedService(
    new SqlOutboxRelay(inventoryCs),
    sp.GetRequiredService<IBus>(),
    sp.GetRequiredService<ILogger<InventoryOutboxRelayHostedService>>()));

builder.Services.AddHostedService<DeadLetterSlackNotifierHostedService>();

await builder.Build().RunAsync();
