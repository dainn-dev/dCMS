using System.Threading.RateLimiting;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Categories;
using dCMS.Catalog.Api.VariantAxes;
using dCMS.Catalog.Api.Middleware;
using dCMS.Catalog.Api.Public;
using dCMS.Catalog.Api.Products;
using dCMS.Core.Caching;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Core.Services;
using dCMS.Core.Pricing;
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

var app = builder.Build();

app.UseCorrelationId();
app.UseForwardedHeaders();
app.UseCors("api");
app.UseMiddleware<HostTenantRoutingMiddleware>();
app.UseDcmsJwtAuthentication(builder.Configuration);
app.UseMiddleware<AuditMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapProductRoutes(builder.Configuration);
app.MapCategoryRoutes(builder.Configuration);
app.MapVariantAxesRoutes(builder.Configuration);
app.MapPublicProductRoutes();

app.MapGet("/health", () => Results.Json(new { data = new { status = "ok" }, meta = (object?)null, error = (object?)null }))
    .WithTags("health")
    .AllowAnonymous()
    .DisableRateLimiting();

app.Run();

// Exposes entry assembly for Microsoft.AspNetCore.Mvc.Testing (Catalog.Api integration tests).
public partial class Program { }
