// Payment API — health + internal create-intent stub for local stack (DAI-301 / DAI-315).

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Text("dCMS.Payment.Api — dev stub (create-intent for Order service).\n", "text/plain"));

app.MapPost(
    "/internal/payment/create-intent",
    (CreateIntentStubRequest req) =>
    {
        if (string.IsNullOrWhiteSpace(req.OrderId))
        {
            return Results.Json(
                new
                {
                    data = (object?)null,
                    error = new { code = "INVALID_ORDER", message = "orderId is required." },
                });
        }

        var intentId = $"pi_stub_{req.OrderId}";
        var url = $"https://checkout.local/pay/{Uri.EscapeDataString(req.OrderId)}";
        return Results.Json(new { data = new { paymentIntentId = intentId, paymentUrl = url }, error = (object?)null });
    });

app.Run();

internal sealed record CreateIntentStubRequest(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal Amount,
    string Currency);
