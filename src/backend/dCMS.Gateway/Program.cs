using System.Threading.RateLimiting;
using dCMS.Gateway;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Prometheus;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// ── Options ───────────────────────────────────────────────────────────────────
var authOpt = builder.Configuration
    .GetSection(GatewayAuthOptions.SectionName)
    .Get<GatewayAuthOptions>() ?? new GatewayAuthOptions();

if (authOpt.Enabled &&
    (string.IsNullOrWhiteSpace(authOpt.JwtSigningKey) || authOpt.JwtSigningKey.Length < 32))
    throw new InvalidOperationException(
        "Auth:Enabled is true but Auth:JwtSigningKey is missing or shorter than 32 characters.");

builder.Services.Configure<GatewayAuthOptions>(
    builder.Configuration.GetSection(GatewayAuthOptions.SectionName));

// ── YARP (DAI-580) ────────────────────────────────────────────────────────────
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ── Swagger aggregator ────────────────────────────────────────────────────────
builder.Services.AddHttpClient("swagger-aggregator")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<GatewaySwaggerAggregator>();

// Minimal Swagger doc for the gateway's own /health endpoint
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("gateway", new OpenApiInfo
    {
        Title   = "dCMS Gateway API",
        Version = "v1",
        Description = "Aggregated OpenAPI spec — all backoffice and storefront routes proxied through dCMS.Gateway.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "dCMS JWT token.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

// ── CORS (DAI-582) ────────────────────────────────────────────────────────────
var origins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(o => o.AddPolicy("gateway", p =>
{
    if (origins.Length == 0)
        p.SetIsOriginAllowed(_ => false);
    else
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

// ── Rate limiting (DAI-582) ───────────────────────────────────────────────────
var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit",  500);
var windowSecs  = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            data  = (object?)null,
            meta  = (object?)null,
            error = new { code = "rate_limit_exceeded", message = "Too many requests. Please slow down." },
        });
    };

    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        // Partition by tenant_id claim when authenticated, fall back to remote IP
        var partitionKey = http.User.FindFirst("tenant_id")?.Value
                        ?? http.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = permitLimit,
                Window               = TimeSpan.FromSeconds(windowSecs),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
                AutoReplenishment    = true,
            });
    });
});

// ── Forwarded headers ─────────────────────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors("gateway");

// DAI-581: validate incoming token, mint internal JWT, overwrite Authorization header
// Must run BEFORE exception handler so 401 responses are written directly (not caught as 500)
if (authOpt.Enabled)
    app.UseMiddleware<GatewayAuthMiddleware>();

app.UseRateLimiter();

// Exception handler for unexpected errors from YARP / upstreams
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new
    {
        data  = (object?)null,
        meta  = (object?)null,
        error = new { code = "internal_error", message = "An internal error occurred." },
    });
}));

// ── Health ────────────────────────────────────────────────────────────────────
app.MapGet("/health",
    () => Results.Json(new { data = new { status = "ok" }, meta = (object?)null, error = (object?)null }))
    .AllowAnonymous()
    .DisableRateLimiting();

// ── Swagger — aggregated specs + UI ──────────────────────────────────────────
// GET /swagger                          → Swagger UI (multi-spec dropdown)
// GET /swagger/backoffice/swagger.json  → merged backoffice spec (/gateway/v1/...)
// GET /swagger/storefront/swagger.json  → merged storefront spec (/storefront/v1/...)
app.MapGet("/swagger/backoffice/swagger.json",
    async (GatewaySwaggerAggregator agg, CancellationToken ct) =>
        Results.Text(await agg.GetBackofficeSpecAsync(ct), "application/json"))
    .AllowAnonymous().DisableRateLimiting();

app.MapGet("/swagger/storefront/swagger.json",
    async (GatewaySwaggerAggregator agg, CancellationToken ct) =>
        Results.Text(await agg.GetStorefrontSpecAsync(ct), "application/json"))
    .AllowAnonymous().DisableRateLimiting();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/backoffice/swagger.json",  "Backoffice API — all services");
    c.SwaggerEndpoint("/swagger/storefront/swagger.json",  "Storefront API — public + customer");
    c.RoutePrefix   = "swagger";
    c.DocumentTitle = "dCMS Gateway — API Docs";
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.DefaultModelsExpandDepth(-1);
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
});

// ── Prometheus metrics ────────────────────────────────────────────────────────
app.MapMetrics("/metrics").AllowAnonymous().DisableRateLimiting();

// ── YARP proxy (backoffice + storefront) ──────────────────────────────────────
app.MapReverseProxy();

app.Run();

// Expose for WebApplicationFactory in integration tests (DAI-585)
public partial class Program { }
