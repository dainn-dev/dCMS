using dCMS.AspNetCore.Auth;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Infrastructure.Billing;
using dCMS.Infrastructure.Middleware;
using dCMS.Infrastructure.Platform;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.Web;
using dCMS.Order.Api.Routes;
using dCMS.Order.Infrastructure;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.EnsureProductionAuthGuard(builder.Environment);

if (builder.Configuration.IsDcmsAuthEnabled())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
else
{
    builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy(DcmsPolicies.OrderAccess, p => p.RequireAssertion(_ => true));
        o.AddPolicy(DcmsPolicies.OrderDlqAdmin, p => p.RequireAssertion(_ => true));
        o.AddPolicy(DcmsPolicies.OrderFailureManage, p => p.RequireAssertion(_ => true));
    });
}

builder.Services.AddDcmsImpersonationAudit(builder.Configuration);
builder.Services.AddDcmsCors(builder.Configuration);
builder.Services.AddDcmsRateLimiting(
    builder.Configuration,
    DcmsRateLimitingPartitionKeys.FromTenantHeaderOrRemoteIp);

var redisCs = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisCs))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));

builder.Services.AddDcmsTenantEntitlements(builder.Configuration);
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Catalog")))
    builder.Services.AddDcmsPlatformScale(builder.Configuration);
builder.Services.AddOrderInfrastructure(builder.Configuration);
builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "order-api");

// DAI-327: paths containing "/api/orders" → POST create + POST …/cancel (shared prefix).
builder.Services.Configure<IdempotencyOptions>(o =>
{
    o.RequireApiV1Prefix = false;
    o.UseStandardApiEnvelope = false;
    o.PathSubstrings = ["/api/orders", "/api/v1/checkout"];
});

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Order API", Version = "v1", Description = "Order placement, tracking and management endpoints." });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order API v1"); c.RoutePrefix = "swagger"; });

app.UseDcmsCorrelationId();
app.UseDcmsRequestObservability("order-api");
app.UseCors(DcmsWebHostDefaults.CorsPolicyName);
if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.UseDcmsImpersonationAudit();
app.UseRateLimiter();

app.UseMiddleware<IdempotencyMiddleware>();
app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
app.MapDcmsPrometheusMetrics();
app.MapCartHttpRoutes();
app.MapOrderHttpRoutes();
app.MapOrderReturnRoutes();
app.MapOrderReportRoutes();
app.MapOrderFailedRoutes();
app.MapOrderDlqAdminRoutes();
app.MapShipmentWebhookRoutes();
app.MapGet("/", () => Results.Text(
    "dCMS.Order.Api — M5 Order Service (US-18/21: POST+GET /api/orders, POST cancel; DAI-327 idempotency; US-21 JWT RBAC when Auth:Enabled).\n",
    "text/plain"));
app.Run();

public partial class Program;
