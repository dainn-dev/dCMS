using dCMS.Catalog.Worker.Consumers;
using dCMS.Catalog.Worker.Indexing;
using dCMS.Catalog.Worker.Workers;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Infrastructure.Catalog;
using dCMS.Infrastructure.Outbox;
using dCMS.Infrastructure.Search;
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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductCreatedIndexConsumer>();
    x.AddConsumer<ProductUpdatedIndexConsumer>();
    x.AddConsumer<ProductPublishedIndexConsumer>();
    x.AddConsumer<ProductArchivedIndexConsumer>();
    x.AddConsumer<StockUpdatedIndexConsumer>();

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
