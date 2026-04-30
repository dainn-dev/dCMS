using System.Threading.RateLimiting;
using dCMS.AspNetCore.Auth;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Core.Persistence;
using dCMS.Infrastructure.Audit;
using dCMS.Infrastructure.Catalog;
using dCMS.Infrastructure.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.RateLimiting;
using MassTransit;
using dCMS.Promotions.Api.Campaigns;
using dCMS.Promotions.Api.Evaluator;
using dCMS.Promotions.Api.Evaluator.Mechanics;
using dCMS.Promotions.Api.Internal;
using dCMS.Promotions.Api.Migrations;
using dCMS.Promotions.Api.PromoCodes;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var promotionsCs = builder.Configuration.GetConnectionString("Promotions");
if (string.IsNullOrWhiteSpace(promotionsCs))
    throw new InvalidOperationException("Configure ConnectionStrings:Promotions.");

builder.Services.AddHostedService<PromotionsDbMigrationHostedService>();
builder.Services.Configure<InternalPromotionsOptions>(
    builder.Configuration.GetSection(InternalPromotionsOptions.SectionName));

// ── Persistence ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICampaignPersistence>(_ => new SqlCampaignPersistence(promotionsCs));
builder.Services.AddSingleton<IPromoCodePersistence>(_ => new SqlPromoCodePersistence(promotionsCs));
builder.Services.AddSingleton<IPromoCodeRedemptionPersistence>(_ => new SqlPromoCodeRedemptionPersistence(promotionsCs));

// ── Promotion evaluator (DAI-679) ─────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EvaluateIdempotencyCache>(sp => new EvaluateIdempotencyCache(
    sp.GetService<IConnectionMultiplexer>(),
    sp.GetRequiredService<ILogger<EvaluateIdempotencyCache>>()));
builder.Services.AddSingleton<ActiveCampaignsCache>(sp => new ActiveCampaignsCache(
    sp.GetService<IConnectionMultiplexer>(),
    sp.GetRequiredService<ILogger<ActiveCampaignsCache>>()));
builder.Services.AddSingleton<IMechanicEvaluator, ProductDiscountMechanic>();
builder.Services.AddSingleton<IMechanicEvaluator, MixMatchMechanic>();
builder.Services.AddSingleton<IMechanicEvaluator, PwpItemMechanic>();
builder.Services.AddSingleton<IMechanicEvaluator, PwpDiscountMechanic>();
builder.Services.AddSingleton<IMechanicEvaluator, AfterSalesMechanic>();
builder.Services.AddSingleton<PromoCodeCache>(sp => new PromoCodeCache(
    sp.GetService<IConnectionMultiplexer>(),
    sp.GetRequiredService<ILogger<PromoCodeCache>>()));
builder.Services.AddSingleton<PromoCodeResolver, DefaultPromoCodeResolver>();
builder.Services.AddSingleton<IPromotionEvaluator, PromotionEvaluator>();

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
builder.Services.AddDcmsImpersonationAudit(builder.Configuration);

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
// Phase C + P1 #4: AuditLogChannel → local AuditOutbox table → MassTransit publish (at-least-once).
// AuditLogConsumer (Catalog.Worker) persists to dcms_catalog AuditLogs.
builder.Services.AddSingleton<AuditLogChannel>();
builder.Services.AddSingleton(_ => new AuditOutboxPersistence(promotionsCs));
builder.Services.AddHostedService<AuditOutboxWriterBackgroundService>();
builder.Services.AddHostedService<AuditOutboxRelayBackgroundService>();

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
        Description = "Campaigns and promo codes: CRUD, workflow transitions, and history.",
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
app.UseDcmsImpersonationAudit();
app.UseMiddleware<AuditMiddleware>();
app.UseRateLimiter();

// ── Routes ────────────────────────────────────────────────────────────────────
app.MapCampaignRoutes(builder.Configuration);
app.MapPromoCodeRoutes(builder.Configuration);
app.MapEvaluateRoutes(builder.Configuration);
app.MapRedemptionRoutes(builder.Configuration);
app.MapInternalPromotionsRoutes();

app.MapGet("/health", () => Results.Json(new { data = new { status = "ok" }, meta = (object?)null, error = (object?)null }))
    .WithTags("health")
    .AllowAnonymous()
    .DisableRateLimiting();

app.MapDcmsPrometheusMetrics();

app.Run();

// Exposes entry assembly for Microsoft.AspNetCore.Mvc.Testing (Promotions.Api integration tests).
public partial class PromotionsApiProgram { }
