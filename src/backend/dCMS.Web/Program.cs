using dCMS.Web.CatalogProxy;
using dCMS.Web.InventoryProxy;
using Microsoft.AspNetCore.HttpOverrides;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CatalogProxyOptions>(builder.Configuration.GetSection(CatalogProxyOptions.SectionName));
builder.Services.Configure<InventoryProxyOptions>(builder.Configuration.GetSection(InventoryProxyOptions.SectionName));
builder.Services.AddSingleton<CatalogJwtIssuer>();
builder.Services.AddHttpClient("dcmsCatalog");
builder.Services.AddHttpClient("dcmsInventory");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

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

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
