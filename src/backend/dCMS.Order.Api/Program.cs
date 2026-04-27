using dCMS.AspNetCore.Auth;
using dCMS.Infrastructure.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Order.Api.Routes;
using dCMS.Order.Infrastructure;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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

var redisCs = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisCs))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));

builder.Services.AddOrderInfrastructure(builder.Configuration);

// DAI-327: paths containing "/api/orders" → POST create + POST …/cancel (shared prefix).
builder.Services.Configure<IdempotencyOptions>(o =>
{
    o.RequireApiV1Prefix = false;
    o.UseStandardApiEnvelope = false;
    o.PathSubstrings = ["/api/orders"];
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

if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.UseMiddleware<IdempotencyMiddleware>();
app.MapHealthChecks("/health");
app.MapDcmsPrometheusMetrics();
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
