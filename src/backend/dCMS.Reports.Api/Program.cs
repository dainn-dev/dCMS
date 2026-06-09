using dCMS.AspNetCore.Auth;
using dCMS.Infrastructure.Billing;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.Web;
using dCMS.Reports.Api.Routes;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Auth: align with other services (Order/Catalog/etc).
if (builder.Configuration.IsDcmsAuthEnabled())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
else
{
    builder.Services.AddAuthorization(o =>
    {
        // Dev-only: allow everything when auth disabled.
        o.AddPolicy(DcmsPolicies.OrderAccess, p => p.RequireAssertion(_ => true));
    });
}

builder.Services.AddDcmsImpersonationAudit(builder.Configuration);
builder.Services.AddDcmsCors(builder.Configuration);
builder.Services.AddDcmsRateLimiting(
    builder.Configuration,
    DcmsRateLimitingPartitionKeys.FromTenantHeaderOrRemoteIp);

builder.Services.AddDcmsTenantEntitlements(builder.Configuration);
builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Reports API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

builder.Services.AddSingleton<AnalyticsReportQueryStore>();
// DAI-711: delivery-slots report reads the Fulfillment read-model (cross-DB, optional connstring).
builder.Services.AddSingleton<FulfillmentReportQueryStore>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Reports API v1"); c.RoutePrefix = "swagger"; });

app.UseDcmsCorrelationId();
app.UseDcmsRequestObservability("reports-api");
app.UseCors(DcmsWebHostDefaults.CorsPolicyName);
if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.UseDcmsImpersonationAudit();
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
app.MapDcmsPrometheusMetrics();

app.MapReportsRoutes();

app.MapGet("/", () => Results.Text("dCMS.Reports.Api — analytics read-model reporting.\n", "text/plain"));
app.Run();

public partial class Program;

/// <summary>Entry assembly marker for Microsoft.AspNetCore.Mvc.Testing.</summary>
public sealed class ReportsApiAssemblyMarker;

