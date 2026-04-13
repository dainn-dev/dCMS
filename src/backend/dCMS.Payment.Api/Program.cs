// Payment API — US-23 service host (DAI-338+).

using dCMS.Infrastructure.Monitoring;
using dCMS.Payment.Api.Routes;
using dCMS.Payment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddPaymentInfrastructure(builder.Configuration);
builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "payment");

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapDcmsPrometheusMetrics();
app.MapPaymentGatewayWebhookRoutes();
app.MapGet("/", () => Results.Text("dCMS.Payment.Api — create-intent + PaymentTransactions + webhooks (DAI-339 / DAI-341).\n", "text/plain"));

app.MapPost(
    "/internal/payment/create-intent",
    async (CreateIntentRequest req, CreatePaymentIntentService svc, CancellationToken ct) =>
    {
        var method = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "card" : req.PaymentMethod.Trim();
        var outcome = await svc.ExecuteAsync(
            req.OrderId,
            req.TenantId,
            req.StoreId,
            req.CustomerId,
            req.Amount,
            req.Currency,
            method,
            ct).ConfigureAwait(false);

        if (outcome is CreatePaymentIntentOutcome.Success ok)
        {
            return Results.Json(
                new { data = new { paymentIntentId = ok.PaymentIntentId, paymentUrl = ok.PaymentUrl }, error = (object?)null });
        }

        if (outcome is CreatePaymentIntentOutcome.ValidationError err)
        {
            return Results.Json(new { data = (object?)null, error = new { code = err.Code, message = err.Message } });
        }

        throw new InvalidOperationException($"Unexpected outcome: {outcome.GetType().Name}");
    });

app.Run();

internal sealed record CreateIntentRequest(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string? PaymentMethod);
