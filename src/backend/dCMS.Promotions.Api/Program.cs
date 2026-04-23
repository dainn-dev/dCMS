using System.Threading.RateLimiting;
using dCMS.AspNetCore.Auth;
using dCMS.Core.Persistence;
using dCMS.Infrastructure.Audit;
using dCMS.Infrastructure.Catalog;
using dCMS.Infrastructure.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.RateLimiting;
using dCMS.Promotions.Api.Campaigns;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var catalogCs = builder.Configuration.GetConnectionString("Catalog");
if (string.IsNullOrWhiteSpace(catalogCs))
    throw new InvalidOperationException("Configure ConnectionStrings:Catalog (PostgreSQL catalog database).");

// ── Persistence ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICampaignPersistence>(_ => new SqlCampaignPersistence(catalogCs));

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddDcmsJwtAuthentication(builder.Configuration);

// ── Rate limiting ─────────────────────────────────────────────────────────────
var redisCs = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisCs))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));

builder.Services.AddSingleton(sp => new TenantPlanRateLimit(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetService<IConnectionMultiplexer>()));

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
                AutoReplenishment = true,
            });
    });
});

// ── Audit log ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AuditLogChannel>();
builder.Services.AddSingleton(_ => new SqlAuditLogPersistence(catalogCs));
builder.Services.AddHostedService<AuditLogBackgroundService>();

// ── CORS ──────────────────────────────────────────────────────────────────────
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("api", p =>
{
    if (origins.Length == 0) p.SetIsOriginAllowed(_ => false);
    else p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

// ── RabbitMQ DLQ monitoring ───────────────────────────────────────────────────
builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "promotions");

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "dCMS Promotions API",
        Version = "v1",
        Description = "Campaign management: CRUD, workflow transitions, and history.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "dCMS JWT issued by dcms-gateway.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseExceptionHandler(errApp => errApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        data  = (object?)null,
        meta  = (object?)null,
        error = new { code = "internal_error", message = "An internal error occurred." },
    });
}));

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Promotions API v1");
    c.RoutePrefix = "swagger";
});

app.UseForwardedHeaders();
app.UseCors("api");
app.UseMiddleware<HostTenantRoutingMiddleware>();
app.UseDcmsJwtAuthentication(builder.Configuration);
app.UseMiddleware<AuditMiddleware>();
app.UseRateLimiter();

// ── Routes ────────────────────────────────────────────────────────────────────
app.MapCampaignRoutes(builder.Configuration);

app.MapGet("/health", () => Results.Json(new { data = new { status = "ok" }, meta = (object?)null, error = (object?)null }))
    .WithTags("health")
    .AllowAnonymous()
    .DisableRateLimiting();

app.MapDcmsPrometheusMetrics();

app.Run();

// Exposes entry assembly for Microsoft.AspNetCore.Mvc.Testing (Promotions.Api integration tests).
public partial class PromotionsApiProgram { }
