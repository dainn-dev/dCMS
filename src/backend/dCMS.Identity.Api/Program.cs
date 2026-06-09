using dCMS.AspNetCore.Auth;
using dCMS.AspNetCore.Auth.Middleware;
using dCMS.Identity.Api.Migrations;
using dCMS.Identity.Api.Persistence;
using dCMS.Identity.Api.Routes;
using dCMS.Identity.Api.Security;
using dCMS.Infrastructure.Monitoring;
using dCMS.Infrastructure.Web;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Bind timestamptz columns to DateTimeOffset (incl. positional records). See DapperTypeHandlers.
dCMS.Infrastructure.Persistence.DapperTypeHandlers.Register();

var connectionString = builder.Configuration.GetConnectionString("Identity");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:Identity.");

builder.Services.AddHostedService<IdentityDbMigrationHostedService>();

if (builder.Configuration.IsDcmsAuthEnabled())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
else
    builder.Services.AddAuthorization();

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Identity API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

builder.Services.AddSingleton<IAuthUserStore>(_ => new SqlAuthUserStore(connectionString));
builder.Services.AddSingleton<IImpersonationLogStore>(_ => new SqlImpersonationLogStore(connectionString));
builder.Services.AddSingleton<DcmsPasswordHasher>();

var authOptions = builder.Configuration.GetSection(DcmsAuthOptions.SectionName).Get<DcmsAuthOptions>() ?? new DcmsAuthOptions();
var staticClientId = builder.Configuration.GetSection("Dcms:Client")["Id"]?.Trim();
if (builder.Configuration.IsDcmsAuthEnabled())
{
    if (string.IsNullOrWhiteSpace(staticClientId))
        throw new InvalidOperationException("Dcms:Client.Id is required when Auth:Enabled is true.");
    if (string.IsNullOrWhiteSpace(authOptions.JwtSigningKey) || authOptions.JwtSigningKey.Length < 32)
        throw new InvalidOperationException("Auth:JwtSigningKey is required (>= 32 chars) for Identity.Api to mint tokens.");
}

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(sp => new JwtMinter(authOptions, staticClientId ?? "dev"));

builder.Services.AddDcmsImpersonationAudit(builder.Configuration);
builder.Services.AddDcmsCors(builder.Configuration);
builder.Services.AddDcmsRateLimiting(
    builder.Configuration,
    DcmsRateLimitingPartitionKeys.FromTenantHeaderOrRemoteIp);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity API v1"); c.RoutePrefix = "swagger"; });

app.UseDcmsCorrelationId();
app.UseDcmsRequestObservability("identity-api");
app.UseCors(DcmsWebHostDefaults.CorsPolicyName);
if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.UseDcmsImpersonationAudit();
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
app.MapDcmsPrometheusMetrics();

app.MapLoginRoutes();
app.MapAdminImpersonateRoutes();

app.MapGet("/", () => Results.Text("dCMS.Identity.Api\n", "text/plain"))
    .AllowAnonymous()
    .DisableRateLimiting();
app.Run();

public partial class Program;
