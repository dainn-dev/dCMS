using System.IO;
using dCMS.Infrastructure.Monitoring;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Persist antiforgery / auth cookie keys across Docker recreates (default is /root/.aspnet, ephemeral in containers).
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "umbraco", "Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("dCMS.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ── YARP: forward /gateway/** → dCMS.Gateway ─────────────────────────────────
// GatewayUrl must be dCMS.Gateway (YARP), e.g. http://localhost:5100 — not Order/Catalog/etc.
// If this points at Order.Api, paths like /gateway/v1/orders/orders are forwarded unchanged and return 404 upstream.
// Override via Dcms:GatewayUrl in appsettings or environment variable.
var gatewayUrl = builder.Configuration["Dcms:GatewayUrl"] ?? "http://localhost:5100";
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes:
        [
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "gateway-route",
                ClusterId = "gateway-cluster",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "/gateway/{**catch-all}" },
            }
        ],
        clusters:
        [
            new Yarp.ReverseProxy.Configuration.ClusterConfig
            {
                ClusterId = "gateway-cluster",
                Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                {
                    ["primary"] = new Yarp.ReverseProxy.Configuration.DestinationConfig
                    {
                        Address = gatewayUrl,
                    }
                }
            }
        ]);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

app.UseForwardedHeaders();

app.MapGet(
    "/health",
    () =>
        Results.Json(
            new
            {
                data = new { status = "ok", service = "dcms-web" },
                meta = (object?)null,
                error = (object?)null,
            }));

app.MapDcmsPrometheusMetrics();

// ── Map YARP proxy BEFORE Umbraco so /gateway/** is intercepted first ─────────
app.MapReverseProxy();

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

