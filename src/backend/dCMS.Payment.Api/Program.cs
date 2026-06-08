// Payment API - US-23 service host (DAI-338+).

using System.Security.Claims;
using System.Threading.RateLimiting;
using dCMS.AspNetCore.Auth;
using dCMS.Infrastructure.Monitoring;
using dCMS.Payment.Api.Routes;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure;
using dCMS.Payment.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddPaymentInfrastructure(builder.Configuration);
builder.Services.AddRabbitMqDlqMonitoring(builder.Configuration, "payment");

if (builder.Configuration.GetSection("Auth").Exists())
    builder.Services.AddDcmsJwtAuthentication(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var tenant = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var key = string.IsNullOrWhiteSpace(tenant)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : tenant.Trim();

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 500),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("api", p =>
{
    if (origins.Length == 0) p.SetIsOriginAllowed(_ => false);
    else p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseCors("api");
if (builder.Configuration.GetSection("Auth").Exists())
    app.UseDcmsJwtAuthentication(builder.Configuration);
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
    await next().ConfigureAwait(false);
});

app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
app.MapDcmsPrometheusMetrics();
app.MapPaymentGatewayWebhookRoutes();
app.MapGet(
        "/",
        () => Results.Text(
            "dCMS.Payment.Api - create-intent + PaymentTransactions + webhooks (DAI-339 / DAI-341).\n",
            "text/plain"))
    .AllowAnonymous()
    .DisableRateLimiting();

var internalRoutes = app.MapGroup("/internal/payment")
    .AddEndpointFilter<PaymentInternalAuthEndpointFilter>();

internalRoutes.MapPost(
    "/create-intent",
    async (CreateIntentRequest req, CreatePaymentIntentService svc, IConfiguration configuration, CancellationToken ct) =>
    {
        var method = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "card" : req.PaymentMethod.Trim();
        var outcome = await svc.ExecuteAsync(
            ResolveClientId(configuration),
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
            return Results.Json(
                new { data = (object?)null, error = new { code = err.Code, message = err.Message } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        throw new InvalidOperationException($"Unexpected outcome: {outcome.GetType().Name}");
    });

internalRoutes.MapPost(
    "/{chargeRef}/capture",
    async (
        string chargeRef,
        PaymentOperationRequest req,
        IPaymentGateway gateway,
        IPaymentTransactionRepository repository,
        IConfiguration configuration,
        CancellationToken ct) =>
    {
        var scope = await ResolveTransactionScopeAsync(chargeRef, req, repository, configuration, ct).ConfigureAwait(false);
        if (scope.Error is not null)
            return scope.Error;

        var row = scope.Transaction!;
        var result = await gateway.ProcessPaymentAsync(
                new ProcessPaymentGatewayRequest(
                    row.PaymentIntentId,
                    row.OrderId,
                    row.TenantId,
                    row.Amount,
                    row.Currency,
                    row.PaymentMethod),
                ct)
            .ConfigureAwait(false);

        return result switch
        {
            ProcessPaymentGatewayResult.Succeeded s => await CompleteCaptureAsync(repository, row, s.ProviderPaymentId, ct),
            ProcessPaymentGatewayResult.AlreadySucceeded a => await CompleteCaptureAsync(repository, row, a.ProviderPaymentId, ct),
            ProcessPaymentGatewayResult.Failed f => Results.BadRequest(
                new { data = (object?)null, error = new { code = f.ErrorCode, message = f.ErrorCode } }),
            _ => Results.BadRequest(new { data = (object?)null, error = new { code = "unknown", message = "unknown gateway result" } }),
        };
    });

internalRoutes.MapPost(
    "/{chargeRef}/refund",
    async (
        string chargeRef,
        PaymentOperationRequest req,
        IPaymentGateway gateway,
        IPaymentTransactionRepository repository,
        IConfiguration configuration,
        CancellationToken ct) =>
    {
        var scope = await ResolveTransactionScopeAsync(chargeRef, req, repository, configuration, ct).ConfigureAwait(false);
        if (scope.Error is not null)
            return scope.Error;

        var row = scope.Transaction!;
        var result = await gateway.RefundPaymentAsync(
                new RefundPaymentGatewayRequest(
                    row.PaymentIntentId,
                    row.OrderId,
                    row.TenantId,
                    req.Amount ?? row.Amount,
                    row.Currency,
                    req.Reason ?? "refund"),
                ct)
            .ConfigureAwait(false);

        return result switch
        {
            RefundPaymentGatewayResult.Succeeded s => await CompleteRefundAsync(repository, row, s.RefundId, ct),
            RefundPaymentGatewayResult.AlreadyRefunded a => await CompleteRefundAsync(repository, row, a.RefundId, ct),
            RefundPaymentGatewayResult.Failed f => Results.BadRequest(
                new { data = (object?)null, error = new { code = f.ErrorCode, message = f.ErrorCode } }),
            _ => Results.BadRequest(new { data = (object?)null, error = new { code = "unknown", message = "unknown gateway result" } }),
        };
    });

internalRoutes.MapPost(
    "/{chargeRef}/void",
    async (
        string chargeRef,
        PaymentOperationRequest req,
        IPaymentTransactionRepository repository,
        IConfiguration configuration,
        CancellationToken ct) =>
    {
        var scope = await ResolveTransactionScopeAsync(chargeRef, req, repository, configuration, ct).ConfigureAwait(false);
        if (scope.Error is not null)
            return scope.Error;

        return Results.Json(new { data = new { chargeRef }, error = (object?)null });
    });

app.Run();

static async Task<IResult> CompleteCaptureAsync(
    IPaymentTransactionRepository repository,
    PaymentTransaction row,
    string providerPaymentId,
    CancellationToken cancellationToken)
{
    await repository
        .UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, row.ClientId, row.Provider, "completed", cancellationToken)
        .ConfigureAwait(false);
    return Results.Json(new { data = new { chargeRef = providerPaymentId }, error = (object?)null });
}

static async Task<IResult> CompleteRefundAsync(
    IPaymentTransactionRepository repository,
    PaymentTransaction row,
    string refundId,
    CancellationToken cancellationToken)
{
    await repository
        .UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, row.ClientId, row.Provider, "refunded", cancellationToken)
        .ConfigureAwait(false);
    return Results.Json(new { data = new { refundRef = refundId }, error = (object?)null });
}

static async Task<TransactionScopeResult> ResolveTransactionScopeAsync(
    string chargeRef,
    PaymentOperationRequest req,
    IPaymentTransactionRepository repository,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(chargeRef))
        return TransactionScopeResult.Fail(StatusCodes.Status400BadRequest, "INVALID_CHARGE_REF", "chargeRef is required.");

    if (!Guid.TryParse(req.TenantId, out var tenantId))
        return TransactionScopeResult.Fail(StatusCodes.Status400BadRequest, "INVALID_TENANT", "tenantId must be a valid UUID.");

    Guid? storeId = null;
    if (!string.IsNullOrWhiteSpace(req.StoreId))
    {
        if (!Guid.TryParse(req.StoreId, out var parsedStoreId))
            return TransactionScopeResult.Fail(StatusCodes.Status400BadRequest, "INVALID_STORE", "storeId must be a valid UUID.");
        storeId = parsedStoreId;
    }

    var row = await repository
        .GetLatestByPaymentIntentIdAsync(chargeRef.Trim(), tenantId, ResolveClientId(configuration), "stub", storeId, cancellationToken)
        .ConfigureAwait(false);
    if (row is null)
        return TransactionScopeResult.Fail(StatusCodes.Status404NotFound, "PAYMENT_NOT_FOUND", "No payment transaction matches the supplied scope.");

    if (req.OrderId.HasValue && req.OrderId.Value != row.OrderId)
        return TransactionScopeResult.Fail(StatusCodes.Status403Forbidden, "ORDER_MISMATCH", "orderId does not match the payment transaction.");
    if (req.Amount.HasValue && req.Amount.Value != row.Amount)
        return TransactionScopeResult.Fail(StatusCodes.Status403Forbidden, "AMOUNT_MISMATCH", "amount does not match the payment transaction.");
    if (!string.IsNullOrWhiteSpace(req.Currency) && !string.Equals(req.Currency, row.Currency, StringComparison.OrdinalIgnoreCase))
        return TransactionScopeResult.Fail(StatusCodes.Status403Forbidden, "CURRENCY_MISMATCH", "currency does not match the payment transaction.");

    return new TransactionScopeResult(row, null);
}

static string ResolveClientId(IConfiguration configuration) =>
    configuration.GetSection("Dcms:Client")["Id"]?.Trim() ?? "aeon";

internal sealed class PaymentInternalAuthEndpointFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var deny = CheckAuth(context.HttpContext, configuration);
        if (deny is not null)
            return deny;
        return await next(context).ConfigureAwait(false);
    }

    /// <summary>Returns a 401 <see cref="IResult"/> if the request is not authorised; null if it should proceed.</summary>
    public static IResult? CheckAuth(HttpContext http, IConfiguration configuration)
    {
        if (http.User.Identity?.IsAuthenticated == true)
            return null;

        var expected = configuration["Payment:InternalApiKey"]?.Trim()
            ?? configuration["Payment:ApiKey"]?.Trim();
        var provided = http.Request.Headers["X-Internal-Api-Key"].FirstOrDefault()
            ?? http.Request.Headers["X-Api-Key"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(expected)
            && string.Equals(provided?.Trim(), expected, StringComparison.Ordinal))
        {
            AddServicePrincipal(http);
            return null;
        }

        return Results.Json(
            new { data = (object?)null, error = new { code = "UNAUTHORIZED", message = "Internal payment authentication is required." } },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static void AddServicePrincipal(HttpContext http)
    {
        var identity = new ClaimsIdentity("PaymentInternalApiKey");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "payment-internal-api-key"));
        http.User = new ClaimsPrincipal(identity);
    }
}

internal sealed record CreateIntentRequest(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string? PaymentMethod);

internal sealed record PaymentOperationRequest(
    string TenantId,
    string? StoreId = null,
    Guid? OrderId = null,
    decimal? Amount = null,
    string? Currency = null,
    string? PaymentMethod = null,
    string? Reason = null);

internal sealed record TransactionScopeResult(PaymentTransaction? Transaction, IResult? Error)
{
    public static TransactionScopeResult Fail(int statusCode, string code, string message) =>
        new(
            null,
            Results.Json(
                new { data = (object?)null, error = new { code, message } },
                statusCode: statusCode));
}

public partial class PaymentApiProgram { }
