using dCMS.AspNetCore.Auth;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Notification.Api.Migrations;
using dCMS.Notification.Api.Rendering;
using dCMS.Notification.Api.Routes;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var notificationCs = builder.Configuration.GetConnectionString("Notification");
if (string.IsNullOrWhiteSpace(notificationCs))
    throw new InvalidOperationException("Configure ConnectionStrings:Notification.");

builder.Services.AddHostedService<NotificationDbMigrationHostedService>();

if (builder.Configuration.IsDcmsAuthEnabled())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
else
{
    builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy(DcmsPolicies.CatalogWrite, p => p.RequireAssertion(_ => true));
    });
}

builder.Services.AddDcmsImpersonationAudit(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Notification API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

builder.Services.AddSingleton<TemplateRepository>();
builder.Services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
builder.Services.AddSingleton<NotificationEventsRepository>();

// DAI-687: config-driven catalog of editable template types (no frontend rebuild to change).
builder.Services.Configure<TemplateCatalogOptions>(builder.Configuration.GetSection("TemplateCatalog"));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification API v1"); c.RoutePrefix = "swagger"; });

if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.UseDcmsImpersonationAudit();

app.MapHealthChecks("/health");
app.MapDcmsPrometheusMetrics();

app.MapTemplateRoutes();
app.MapTemplateCatalogRoutes();
app.MapNotificationFeedRoutes();

app.MapGet("/", () => Results.Text("dCMS.Notification.Api\n", "text/plain"));
app.Run();

public partial class Program;

