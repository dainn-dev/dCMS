using dCMS.AspNetCore.Auth;
using dCMS.Core.Approvals;
using dCMS.Core.Persistence;
using dCMS.Infrastructure.Approvals;
using dCMS.Infrastructure.Monitoring;
using dCMS.Approval.Api.Routes;
using dCMS.Approval.Api.Routes.Subjects;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var catalogCs = builder.Configuration.GetConnectionString("Catalog");
if (string.IsNullOrWhiteSpace(catalogCs))
    throw new InvalidOperationException("Configure ConnectionStrings:Catalog (PostgreSQL catalog database).");

if (builder.Configuration.IsDcmsAuthEnabled())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);
else
{
    builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy(DcmsPolicies.ApprovalManage, p => p.RequireAssertion(_ => true));
    });
}

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "dCMS Approval API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

builder.Services.AddSingleton<IApprovalRequestPersistence>(_ => new SqlApprovalRequestPersistence(catalogCs));

// DAI-718: strategy hook (entity-specific side effects).
builder.Services.AddSingleton<IApprovalSubject>(_ => new CampaignApprovalSubject(catalogCs));
builder.Services.AddSingleton<IApprovalSubject>(_ => new PromoCodeApprovalSubject(catalogCs));
builder.Services.AddSingleton<IApprovalSubject>(_ => new ProductApprovalSubject(catalogCs));

// DAI-721: Content subject — HTTP callback to dCMS.Web (Umbraco) to publish/unpublish on approval.
var contentCallback = new ContentApprovalCallbackOptions
{
    CallbackUrl = builder.Configuration["ContentApproval:CallbackUrl"],
    ApiKey = builder.Configuration["ContentApproval:ApiKey"],
};
builder.Services.AddHttpClient(ContentApprovalCallbackOptions.HttpClientName, c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton(contentCallback);
builder.Services.AddSingleton<IApprovalSubject>(sp =>
    new ContentApprovalSubject(sp.GetRequiredService<IHttpClientFactory>(), contentCallback));

builder.Services.AddSingleton<ApprovalSubjectRegistry>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Approval API v1"); c.RoutePrefix = "swagger"; });

if (app.Configuration.IsDcmsAuthEnabled())
    app.UseDcmsJwtAuthentication(app.Configuration);
else
    app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapDcmsPrometheusMetrics();

app.MapApprovalRoutes();

app.MapGet("/", () => Results.Text("dCMS.Approval.Api\n", "text/plain"));
app.Run();

public partial class Program;

