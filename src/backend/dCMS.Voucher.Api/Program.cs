using dCMS.AspNetCore.Auth;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.Web;
using dCMS.Voucher.Api.Migrations;
using dCMS.Voucher.Api.Persistence;
using dCMS.Voucher.Api.Routes;
using dCMS.Voucher.Api.Workers;
using MassTransit;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.EnsureProductionAuthGuard(builder.Environment);

// Bind timestamptz columns to DateTimeOffset (incl. positional records). See DapperTypeHandlers.
dCMS.Infrastructure.Persistence.DapperTypeHandlers.Register();

var connectionString = builder.Configuration.GetConnectionString("Voucher");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:Voucher.");

builder.Services.AddHostedService<VoucherDbMigrationHostedService>();

if (builder.Configuration.IsDcmsAuthEnabled())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
else
    builder.Services.AddAuthorization();

builder.Services.AddDcmsImpersonationAudit(builder.Configuration);
builder.Services.AddDcmsCors(builder.Configuration);
builder.Services.AddDcmsRateLimiting(
    builder.Configuration,
    DcmsRateLimitingPartitionKeys.FromTenantHeaderOrRemoteIp);

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Voucher API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

builder.Services.AddSingleton<IVoucherStore>(_ => new SqlVoucherStore(connectionString));

builder.Services.AddHostedService<HoldExpiryWorker>();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMq:User"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMq:Password"] ?? "guest";
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Voucher API v1"); c.RoutePrefix = "swagger"; });

app.UseDcmsCorrelationId();
app.UseDcmsRequestObservability("voucher-api");
app.UseCors(DcmsWebHostDefaults.CorsPolicyName);
if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.UseDcmsImpersonationAudit();
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
app.MapDcmsPrometheusMetrics();

app.MapVoucherRoutes();

app.MapGet("/", () => Results.Text("dCMS.Voucher.Api\n", "text/plain"))
    .AllowAnonymous()
    .DisableRateLimiting();
app.Run();

internal partial class Program;
