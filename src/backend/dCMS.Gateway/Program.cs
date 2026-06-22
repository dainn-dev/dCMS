using System.Threading.RateLimiting;
using dCMS.AspNetCore.Auth;
using dCMS.Gateway;
using dCMS.Infrastructure.Billing;
using dCMS.Infrastructure.Routing;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Prometheus;
using Swashbuckle.AspNetCore.SwaggerUI;
using dCMS.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.EnsureProductionAuthGuard(builder.Environment);

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

builder.Services.AddDcmsTenantEntitlements(builder.Configuration);
builder.Services.Configure<GatewayHostRoutingOptions>(
    builder.Configuration.GetSection(GatewayHostRoutingOptions.SectionName));
builder.Services.AddDcmsHostTenantResolution(builder.Configuration);

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
        Description = """
            Aggregated OpenAPI spec for **dCMS** — a multi-tenant headless eCommerce platform.

            All backoffice and storefront routes are proxied through `dCMS.Gateway` (YARP reverse proxy).
            The gateway provides:

            - **Authentication** — validates incoming JWT, mints short-lived internal tokens (`Auth:InternalTokenTtlSeconds`), forwards `tenant_id` + `roles` claims downstream.
            - **Rate limiting** — fixed-window partitioned by `tenant_id` claim or remote IP (defaults: 500 req / 60 s).
            - **CORS** — origins controlled by `Cors:AllowedOrigins`.
            - **Health & metrics** — `GET /health`, `GET /metrics` (Prometheus).

            ### Routing convention

            | Audience    | Prefix                   | Description                                              |
            |-------------|--------------------------|----------------------------------------------------------|
            | Backoffice  | `/gateway/v1/{service}/` | Authenticated admin & operator routes.                   |
            | Storefront  | `/storefront/v1/`        | Public + customer routes (catalog, orders).              |

            ### Tenant scoping

            Every request must include the tenant context. For backoffice callers this is the `tenant_id`
            claim on the JWT; for service-to-service calls the `X-Tenant-Id` header is honored. Cross-tenant
            access is rejected at the gateway.

            ### Error envelope

            All responses use the dCMS envelope shape:

            ```json
            { "data": <payload-or-null>, "meta": <metadata-or-null>, "error": <{code,message}-or-null> }
            ```

            Common error codes: `unauthorized`, `forbidden`, `validation_failed`, `not_found`,
            `rate_limit_exceeded`, `internal_error`.
            """,
        Contact = new OpenApiContact
        {
            Name  = "dCMS Platform Team",
            Email = "platform@dcms.local",
        },
        License = new OpenApiLicense { Name = "Proprietary — internal use only" },
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = """
            dCMS JWT bearer token.

            **Backoffice flow:** obtain via the Auth service after a backoffice login. The token carries
            `tenant_id`, `user_id`, and `roles` claims; the gateway validates the signature, then mints a
            short-lived **internal** JWT (`Auth:InternalTokenTtlSeconds`) it forwards to upstream services
            on the `Authorization` header. Clients never see the internal token.

            **Storefront flow:** for customer-facing endpoints, the token is issued after customer login
            and carries `customer_id` plus the active `tenant_id` (selected store).

            Send as: `Authorization: Bearer <token>`.
            """,
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

// ── CORS (DAI-582) ────────────────────────────────────────────────────────────
var origins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(o => o.AddPolicy("api", p =>
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
        ctx.HttpContext.SetDcmsFailureReason("rate_limited");
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("dCMS.Gateway.RateLimiting");
        var correlationId = ctx.HttpContext.Items.TryGetValue(DcmsWebHostDefaults.CorrelationIdHeaderName, out var value)
            ? value?.ToString()
            : ctx.HttpContext.TraceIdentifier;
        logger.LogWarning(
            "Gateway rate limit rejected {Method} {Path} correlation {CorrelationId} tenant {TenantId} remote {RemoteIp}",
            ctx.HttpContext.Request.Method,
            ctx.HttpContext.Request.Path.Value,
            correlationId,
            ctx.HttpContext.User.FindFirst("tenant_id")?.Value ?? "anonymous",
            ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
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
var logger = app.Logger;

app.UseForwardedHeaders();
app.UseDcmsCorrelationId();
app.UseDcmsRequestObservability("gateway");
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.FirstOrDefault();
    var allowedOrigins = builder.Configuration.GetSection(DcmsWebHostDefaults.CorsOriginsSectionName).Get<string[]>() ?? [];
    if (!string.IsNullOrWhiteSpace(origin) &&
        (allowedOrigins.Length == 0 || !allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
    {
        logger.LogWarning(
            "Gateway rejected CORS origin {Origin} for {Method} {Path} correlation {CorrelationId} remote {RemoteIp}",
            origin,
            context.Request.Method,
            context.Request.Path.Value,
            context.Items.TryGetValue(DcmsWebHostDefaults.CorrelationIdHeaderName, out var value) ? value?.ToString() : context.TraceIdentifier,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    await next().ConfigureAwait(false);
});
app.UseCors("api");

app.UseMiddleware<GatewayHostRoutingMiddleware>();

// DAI-581: validate incoming token, mint internal JWT, overwrite Authorization header
// Must run BEFORE exception handler so 401 responses are written directly (not caught as 500)
if (authOpt.Enabled)
    app.UseMiddleware<GatewayAuthMiddleware>();

if (authOpt.Enabled)
    app.UseMiddleware<GatewayTenantEntitlementMiddleware>();

app.UseRateLimiter();

// Exception handler for unexpected errors from YARP / upstreams
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var exLogger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("dCMS.Gateway.ExceptionHandler");
    var exFeature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var correlationId = ctx.Items.TryGetValue(DcmsWebHostDefaults.CorrelationIdHeaderName, out var value)
        ? value?.ToString()
        : ctx.TraceIdentifier;

    if (exFeature?.Error != null)
    {
        exLogger.LogError(
            exFeature.Error,
            "Gateway unhandled exception {Method} {Path} correlation {CorrelationId}",
            ctx.Request.Method,
            ctx.Request.Path.Value,
            correlationId);
    }

    ctx.SetDcmsFailureReason("internal_error");
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
    .DisableRateLimiting()
    .WithName("gateway-health")
    .WithTags("gateway")
    .WithSummary("Gateway liveness probe.")
    .WithDescription("Returns `{ data: { status: \"ok\" } }` whenever the gateway process is alive. Does not validate upstream service health — use `/metrics` or per-service `/health` for deep checks.");

// ── Swagger — aggregated specs + UI ──────────────────────────────────────────
// GET /swagger                          → Swagger UI (multi-spec dropdown)
// GET /swagger/gateway/swagger.json     → gateway's own minimal spec (health/metrics + global info)
// GET /swagger/backoffice/swagger.json  → merged backoffice spec  (/gateway/v1/...)
// GET /swagger/storefront/swagger.json  → merged storefront spec (/storefront/v1/...)
// Serve the gateway-only Swashbuckle doc explicitly so it doesn't shadow the aggregator routes below.
app.MapGet("/swagger/gateway/swagger.json",
    (Swashbuckle.AspNetCore.Swagger.ISwaggerProvider provider) =>
    {
        var doc = provider.GetSwagger("gateway");
        using var sw = new StringWriter();
        doc.SerializeAsV3(new Microsoft.OpenApi.Writers.OpenApiJsonWriter(sw));
        return Results.Text(sw.ToString(), "application/json");
    })
    .AllowAnonymous().DisableRateLimiting()
    .WithName("gateway-spec")
    .WithDescription("Gateway-only OpenAPI 3 spec — describes /health, /metrics, and global security/info.");

app.MapGet("/swagger/backoffice/swagger.json",
    async (GatewaySwaggerAggregator agg, CancellationToken ct) =>
        Results.Text(await agg.GetBackofficeSpecAsync(ct), "application/json"))
    .AllowAnonymous().DisableRateLimiting()
    .WithName("backoffice-spec")
    .WithDescription("Aggregated OpenAPI 3 spec covering every backoffice route exposed under /gateway/v1/.");

app.MapGet("/swagger/storefront/swagger.json",
    async (GatewaySwaggerAggregator agg, CancellationToken ct) =>
        Results.Text(await agg.GetStorefrontSpecAsync(ct), "application/json"))
    .AllowAnonymous().DisableRateLimiting()
    .WithName("storefront-spec")
    .WithDescription("Aggregated OpenAPI 3 spec for public + customer routes under /storefront/v1/.");

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/gateway/swagger.json",     "Gateway — overview, health, metrics");
    c.SwaggerEndpoint("/swagger/backoffice/swagger.json",  "Backoffice API — all services (/gateway/v1)");
    c.SwaggerEndpoint("/swagger/storefront/swagger.json",  "Storefront API — public + customer (/storefront/v1)");
    c.RoutePrefix          = "swagger";
    c.DocumentTitle        = "dCMS Gateway — API Docs";
    c.HeadContent          = "<style>.swagger-ui .topbar { background-color: #1f2937; }</style>";
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.EnableDeepLinking();
    c.EnableTryItOutByDefault();
    c.DefaultModelsExpandDepth(-1);
    c.DefaultModelExpandDepth(2);
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    c.SupportedSubmitMethods(SubmitMethod.Get, SubmitMethod.Post, SubmitMethod.Put, SubmitMethod.Patch, SubmitMethod.Delete);
});

// ── Prometheus metrics ────────────────────────────────────────────────────────
app.MapMetrics("/metrics").AllowAnonymous().DisableRateLimiting();

// ── YARP proxy (backoffice + storefront) ──────────────────────────────────────
app.MapReverseProxy();

app.Run();

// Expose for WebApplicationFactory in integration tests (DAI-585)
public partial class Program { }
