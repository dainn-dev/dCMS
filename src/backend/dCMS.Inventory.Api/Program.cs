using System.Threading.RateLimiting;
using dCMS.AspNetCore.Auth;
using Microsoft.OpenApi.Models;
using dCMS.Core.Pricing;
using dCMS.Inventory.Api.Internal;
using dCMS.Inventory.Api.Middleware;
using dCMS.Inventory.Api.Stock;
using dCMS.Inventory.Api.Warehouses;
using dCMS.Inventory.Infrastructure;
using dCMS.Infrastructure.Messaging;
using dCMS.Inventory.Persistence;
using dCMS.Inventory.Services;
using dCMS.Infrastructure.Audit;
using dCMS.Infrastructure.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.Pricing;
using dCMS.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var inventoryCs = builder.Configuration.GetConnectionString("Inventory");
if (string.IsNullOrWhiteSpace(inventoryCs))
    throw new InvalidOperationException("Configure ConnectionStrings:Inventory (PostgreSQL inventory database).");

var auditCs = builder.Configuration.GetConnectionString("Audit") ?? inventoryCs;

builder.Services.AddSingleton<IInventoryStockPersistence>(_ => new SqlStockPersistence(inventoryCs));
builder.Services.AddScoped<StockService>();
builder.Services.AddDcmsJwtAuthentication(builder.Configuration);

var redisCs = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisCs))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));

builder.Services.AddSingleton(sp => new TenantPlanRateLimit(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetService<IConnectionMultiplexer>()));
builder.Services.AddSingleton<AuditLogChannel>();
builder.Services.AddSingleton(_ => new SqlAuditLogPersistence(auditCs));
builder.Services.AddHostedService<AuditLogBackgroundService>();
builder.Services.AddSingleton<IPriceChangeAlerter, NoopPriceChangeAlerter>();
builder.Services.Configure<IdempotencyOptions>(o => o.PathSubstrings = ["/stock"]);
builder.Services.Configure<InternalInventoryOptions>(builder.Configuration.GetSection(InternalInventoryOptions.SectionName));
builder.Services.AddHostedService<InventoryDbMigrationHostedService>();
builder.Services.AddProcessedMessagesCleanup(builder.Configuration, "Inventory");

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Inventory API", Version = "v1", Description = "Stock levels, warehouse management and inventory sync endpoints." });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory API v1"); c.RoutePrefix = "swagger"; });

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

app.UseCorrelationId();
app.UseForwardedHeaders();
app.UseCors("api");
app.UseMiddleware<HostTenantRoutingMiddleware>();
app.UseDcmsJwtAuthentication(builder.Configuration);
app.UseMiddleware<AuditMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapStockRoutes(builder.Configuration);
app.MapWarehouseRoutes(builder.Configuration);
app.MapInternalInventoryRoutes();

app.MapGet("/health", () => Results.Json(new { data = new { status = "ok", service = "inventory" }, meta = (object?)null, error = (object?)null }))
    .WithTags("health")
    .AllowAnonymous()
    .DisableRateLimiting();

app.MapDcmsPrometheusMetrics();

app.Run();

// Exposes entry assembly for Microsoft.AspNetCore.Mvc.Testing (Inventory.Api integration tests).
public partial class Program { }
