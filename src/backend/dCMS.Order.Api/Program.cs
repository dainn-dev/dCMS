using dCMS.Order.Api.Routes;
using dCMS.Order.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOrderInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapOrderHttpRoutes();
app.MapGet("/", () => Results.Text("dCMS.Order.Api — M5 Order Service (US-18: POST /api/orders).\n", "text/plain"));
app.Run();
