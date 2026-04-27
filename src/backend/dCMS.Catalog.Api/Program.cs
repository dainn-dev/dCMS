using System.Threading.RateLimiting;
using dCMS.Catalog.Api;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Attributes;
using dCMS.Catalog.Api.Brands;
using dCMS.Catalog.Api.Categories;
using dCMS.Catalog.Api.Imports;
using dCMS.Catalog.Api.Internal;
using dCMS.Catalog.Api.VariantAxes;
using dCMS.Catalog.Api.Middleware;
using dCMS.Catalog.Api.Public;
using dCMS.Catalog.Api.Products;
using dCMS.Catalog.Api.Services;
using dCMS.Catalog.Api.Storage;
using dCMS.Catalog.Api.Stores;
using dCMS.Core.Caching;
using dCMS.Core.Notifications;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Core.Services;
using dCMS.Core.Pricing;
using dCMS.Infrastructure;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.Audit;
using dCMS.Infrastructure.Caching;
using dCMS.Infrastructure.Catalog;
using dCMS.Infrastructure.Middleware;
using dCMS.Infrastructure.Pricing;
using dCMS.Infrastructure.RateLimiting;
using dCMS.Infrastructure.Search;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MassTransit;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var catalogCs = builder.Configuration.GetConnectionString("Catalog");
if (string.IsNullOrWhiteSpace(catalogCs))
    throw new InvalidOperationException("Configure ConnectionStrings:Catalog (PostgreSQL catalog database).");

var esUrl = builder.Configuration["Elasticsearch:Url"];
if (string.IsNullOrWhiteSpace(esUrl))
    throw new InvalidOperationException("Configure Elasticsearch:Url (product search / US-6).");

builder.Services.AddSingleton(_ => new ElasticsearchClientFactory(new Uri(esUrl)));
builder.Services.AddSingleton<ElasticsearchProductSearchService>();
var redisCs = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisCs))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));

if (!string.IsNullOrWhiteSpace(redisCs))
    builder.Services.AddSingleton<IProductPublicDetailCache, RedisProductPublicDetailCache>();
else
    builder.Services.AddSingleton<IProductPublicDetailCache, NoopProductPublicDetailCache>();

builder.Services.AddSingleton<IProductSearchQuery>(sp =>
{
    var inner = sp.GetRequiredService<ElasticsearchProductSearchService>();
    var redis = sp.GetService<IConnectionMultiplexer>();
    if (redis is null)
        return inner;
    return new RedisCachingProductSearchQuery(inner, redis,
        sp.GetRequiredService<ILogger<RedisCachingProductSearchQuery>>());
});

builder.Services.AddSingleton<ICatalogPersistence>(_ => new SqlCatalogPersistence(catalogCs));
builder.Services.AddSingleton<IProductImagePersistence>(_ => new SqlProductImagePersistence(catalogCs));
builder.Services.Configure<CatalogMediaOptions>(builder.Configuration.GetSection("Catalog:Media"));
builder.Services.Configure<ProductImageS3Options>(builder.Configuration.GetSection("Catalog:S3:ProductImages"));
builder.Services.AddSingleton<ProductImageS3Signer>();

// DAI-684: bulk import endpoints + worker contract.
builder.Services.Configure<ImportFileS3Options>(builder.Configuration.GetSection("Catalog:S3:ImportFiles"));
builder.Services.Configure<CatalogImportOptions>(builder.Configuration.GetSection("Catalog:Imports"));
builder.Services.AddSingleton<ImportFileStore>();
builder.Services.AddSingleton<IImportJobPersistence>(_ => new SqlImportJobPersistence(catalogCs));
builder.Services.AddMassTransit(bus =>
{
    bus.SetKebabCaseEndpointNameFormatter();
    bus.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var user = builder.Configuration["RabbitMq:User"] ?? "guest";
        var pass = builder.Configuration["RabbitMq:Pass"] ?? "guest";
        cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
        cfg.ConfigureEndpoints(context);
    });
});
builder.Services.Configure<CatalogNotificationOptions>(builder.Configuration.GetSection(CatalogNotificationOptions.SectionName));
// P2 #6: ProductNotificationSink publishes UserNotificationCreatedV1 — Notification.Worker writes the row.
builder.Services.AddScoped<IProductNotificationSink, ProductNotificationSink>();
builder.Services.Configure<InternalCatalogOptions>(
    builder.Configuration.GetSection(InternalCatalogOptions.SectionName));
builder.Services.AddSingleton<IBrandPersistence>(_ => new SqlBrandPersistence(catalogCs));
builder.Services.AddScoped<ProductService>();
builder.Services.AddDcmsJwtAuthentication(builder.Configuration);

builder.Services.AddSingleton(sp => new TenantPlanRateLimit(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetService<IConnectionMultiplexer>()));
builder.Services.AddSingleton<AuditLogChannel>();
builder.Services.AddSingleton(_ => new SqlAuditLogPersistence(catalogCs));
builder.Services.AddHostedService<AuditLogBackgroundService>();
builder.Services.AddSingleton<IPriceChangeAlerter, NoopPriceChangeAlerter>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var resolver = httpContext.RequestServices.GetRequiredService<TenantPlanRateLimit>();
        var key = resolver.ResolvePartitionKey(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = resolver.ResolvePermitLimit(key),
                Window = resolver.Window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy("PublicSlugCheck", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("api", p =>
{
    if (origins.Length == 0)
        p.SetIsOriginAllowed(_ => false);
    else
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.Configure<IdempotencyOptions>(o => o.PathSubstrings = ["/products"]);
builder.Services.AddHostedService<CatalogDbMigrationHostedService>();
builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "catalog");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "dCMS Catalog API",
        Version = "v1",
        Description = "Brand, Product, Category, Variant, Notification and Public Storefront endpoints.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "dCMS JWT (issued by dcms-gateway or directly by Auth service).",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

var app = builder.Build();

app.UseExceptionHandler(errApp => errApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "internal_error", message = "An internal error occurred." }
    });
}));

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog API v1");
    c.RoutePrefix = "swagger";
});

app.UseCorrelationId();
app.UseForwardedHeaders();
app.UseCors("api");
app.UseMiddleware<HostTenantRoutingMiddleware>();
app.UseDcmsJwtAuthentication(builder.Configuration);
app.UseMiddleware<AuditMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapBrandRoutes(builder.Configuration);
app.MapAttributeRoutes(builder.Configuration);
app.MapProductRoutes(builder.Configuration);
app.MapStoreCatalogSettingsRoutes(builder.Configuration);
// P2 #6: notification feed routes moved to dCMS.Notification.Api (mounted at same /api/v1/.../notifications path).
app.MapProductImageRoutes(builder.Configuration);
app.MapImportJobRoutes(builder.Configuration);
app.MapCategoryRoutes(builder.Configuration);
app.MapVariantAxesRoutes(builder.Configuration);
app.MapPublicProductRoutes();
app.MapInternalCatalogRoutes(catalogCs);

app.MapGet("/health", () => Results.Json(new { data = new { status = "ok" }, meta = (object?)null, error = (object?)null }))
    .WithTags("health")
    .AllowAnonymous()
    .DisableRateLimiting();

app.MapDcmsPrometheusMetrics();

app.Run();

// Exposes entry assembly for Microsoft.AspNetCore.Mvc.Testing (Catalog.Api integration tests).
public partial class Program { }
