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

// DAI-689: capture/refund/void endpoints used by the multi-tender HttpGatewayTenderClient.
// Bodies carry orderId/tenantId/amount/currency so the IPaymentGateway request types
// can be constructed; PaymentIntentId on the route is the idempotency key.

app.MapPost(
    "/internal/payment/{chargeRef}/capture",
    async (string chargeRef, CaptureRequest req, dCMS.Payment.Core.IPaymentGateway gateway, CancellationToken ct) =>
    {
        var providerReq = new dCMS.Payment.Core.ProcessPaymentGatewayRequest(
            chargeRef, req.OrderId, req.TenantId, req.Amount, req.Currency, req.PaymentMethod ?? "card");
        var result = await gateway.ProcessPaymentAsync(providerReq, ct);
        return result switch
        {
            dCMS.Payment.Core.ProcessPaymentGatewayResult.Succeeded s
                => Results.Json(new { data = new { chargeRef = s.ProviderPaymentId }, error = (object?)null }),
            dCMS.Payment.Core.ProcessPaymentGatewayResult.AlreadySucceeded a
                => Results.Json(new { data = new { chargeRef = a.ProviderPaymentId }, error = (object?)null }),
            dCMS.Payment.Core.ProcessPaymentGatewayResult.Failed f
                => Results.BadRequest(new { data = (object?)null, error = new { code = f.ErrorCode, message = f.ErrorCode } }),
            _ => Results.BadRequest(new { data = (object?)null, error = new { code = "unknown", message = "unknown gateway result" } }),
        };
    });

app.MapPost(
    "/internal/payment/{chargeRef}/refund",
    async (string chargeRef, RefundRequest req, dCMS.Payment.Core.IPaymentGateway gateway, CancellationToken ct) =>
    {
        var providerReq = new dCMS.Payment.Core.RefundPaymentGatewayRequest(
            chargeRef, req.OrderId, req.TenantId, req.Amount, req.Currency, req.Reason ?? "refund");
        var result = await gateway.RefundPaymentAsync(providerReq, ct);
        return result switch
        {
            dCMS.Payment.Core.RefundPaymentGatewayResult.Succeeded s
                => Results.Json(new { data = new { refundRef = s.RefundId }, error = (object?)null }),
            dCMS.Payment.Core.RefundPaymentGatewayResult.AlreadyRefunded a
                => Results.Json(new { data = new { refundRef = a.RefundId }, error = (object?)null }),
            dCMS.Payment.Core.RefundPaymentGatewayResult.Failed f
                => Results.BadRequest(new { data = (object?)null, error = new { code = f.ErrorCode, message = f.ErrorCode } }),
            _ => Results.BadRequest(new { data = (object?)null, error = new { code = "unknown", message = "unknown gateway result" } }),
        };
    });

// Void: IPaymentGateway has no Void today. Returns success — keeps the orchestrator surface
// uniform; a real adapter implements the void path against the provider.
app.MapPost("/internal/payment/{chargeRef}/void",
    (string chargeRef) => Results.Json(new { data = new { chargeRef }, error = (object?)null }));

app.Run();

internal sealed record CreateIntentRequest(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string? PaymentMethod);

internal sealed record CaptureRequest(
    Guid OrderId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string? PaymentMethod);

internal sealed record RefundRequest(
    Guid OrderId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string? Reason);
