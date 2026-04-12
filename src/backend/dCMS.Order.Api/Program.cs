// Placeholder Order API for local Docker stack (DAI-301). No domain logic yet.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Text("dCMS.Order.Api — placeholder (M0 local stack).\n", "text/plain"));
app.Run();
