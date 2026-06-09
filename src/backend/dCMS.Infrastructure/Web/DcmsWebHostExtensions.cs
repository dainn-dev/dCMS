using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;
using System.Threading.RateLimiting;
using dCMS.Infrastructure.RateLimiting;

namespace dCMS.Infrastructure.Web;

public static class DcmsWebHostDefaults
{
    public const string CorsPolicyName = "api";
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string FailureReasonItemName = "dcms.failure_reason";
    public const string RateLimitingSectionName = "RateLimiting";
    public const string CorsOriginsSectionName = "Cors:AllowedOrigins";
}

public static class DcmsObservabilityMetrics
{
    private static readonly Counter HttpRequests = Metrics.CreateCounter(
        "dcms_http_requests_total",
        "HTTP requests handled by dCMS services.",
        new CounterConfiguration { LabelNames = ["service", "route", "status", "failure_reason"] });

    private static readonly Counter WebhookFailures = Metrics.CreateCounter(
        "dcms_webhook_failures_total",
        "Rejected or failed webhook deliveries.",
        new CounterConfiguration { LabelNames = ["service", "provider", "reason"] });

    private static readonly Counter WorkerOperations = Metrics.CreateCounter(
        "dcms_worker_operations_total",
        "Worker operations processed by dCMS background services.",
        new CounterConfiguration { LabelNames = ["service", "operation", "status", "failure_reason"] });

    public static void ObserveHttpRequest(string serviceName, string route, int statusCode, string failureReason) =>
        HttpRequests.WithLabels(SanitizeLabel(serviceName), SanitizeLabel(route), statusCode.ToString(), SanitizeLabel(failureReason)).Inc();

    public static void ObserveWebhookFailure(string serviceName, string provider, string reason) =>
        WebhookFailures.WithLabels(SanitizeLabel(serviceName), SanitizeLabel(provider), SanitizeLabel(reason)).Inc();

    public static void ObserveWorkerOperation(string serviceName, string operation, string status, string failureReason = "none") =>
        WorkerOperations.WithLabels(SanitizeLabel(serviceName), SanitizeLabel(operation), SanitizeLabel(status), SanitizeLabel(failureReason)).Inc();

    private static string SanitizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var trimmed = value.Trim();
        return trimmed.Length > 96 ? trimmed[..96] : trimmed;
    }
}

public static class DcmsWebHostExtensions
{
    public static IServiceCollection AddDcmsCors(
        this IServiceCollection services,
        IConfiguration configuration,
        string policyName = DcmsWebHostDefaults.CorsPolicyName,
        bool allowCredentials = false)
    {
        var origins = configuration.GetSection(DcmsWebHostDefaults.CorsOriginsSectionName).Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(policyName, policy =>
        {
            if (origins.Length == 0)
            {
                policy.SetIsOriginAllowed(_ => false);
                return;
            }

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();

            if (allowCredentials)
                policy.AllowCredentials();
        }));

        return services;
    }

    public static IServiceCollection AddDcmsRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<HttpContext, string> partitionKeyResolver,
        Action<HttpContext>? onRejected = null,
        int defaultPermitLimit = 500,
        int defaultWindowSeconds = 60)
    {
        var permitLimit = configuration.GetValue($"{DcmsWebHostDefaults.RateLimitingSectionName}:PermitLimit", defaultPermitLimit);
        var windowSeconds = configuration.GetValue($"{DcmsWebHostDefaults.RateLimitingSectionName}:WindowSeconds", defaultWindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                onRejected?.Invoke(context.HttpContext);
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    data = (object?)null,
                    meta = (object?)null,
                    error = new { code = "rate_limit_exceeded", message = "Too many requests. Please slow down." },
                }, cancellationToken).ConfigureAwait(false);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var partitionKey = partitionKeyResolver(httpContext);
                if (string.IsNullOrWhiteSpace(partitionKey))
                    partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }

    public static IServiceCollection AddDcmsTenantPlanRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RateLimiterOptions>? configureAdditionalPolicies = null,
        Action<HttpContext>? onRejected = null)
    {
        services.AddSingleton(sp => new TenantPlanRateLimit(
            configuration,
            sp.GetService<StackExchange.Redis.IConnectionMultiplexer>()));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                onRejected?.Invoke(context.HttpContext);
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    data = (object?)null,
                    meta = (object?)null,
                    error = new { code = "rate_limit_exceeded", message = "Too many requests. Please slow down." },
                }, cancellationToken).ConfigureAwait(false);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var resolver = httpContext.RequestServices.GetRequiredService<TenantPlanRateLimit>();
                var partitionKey = resolver.ResolvePartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = resolver.ResolvePermitLimit(partitionKey),
                        Window = resolver.Window,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });

            configureAdditionalPolicies?.Invoke(options);
        });

        return services;
    }

    public static IApplicationBuilder UseDcmsCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var correlationId = ResolveCorrelationId(context);
            context.TraceIdentifier = correlationId;
            context.Items[DcmsWebHostDefaults.CorrelationIdHeaderName] = correlationId;
            context.Request.Headers[DcmsWebHostDefaults.CorrelationIdHeaderName] = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[DcmsWebHostDefaults.CorrelationIdHeaderName] = correlationId;
                return Task.CompletedTask;
            });

            await next().ConfigureAwait(false);
        });

    public static IApplicationBuilder UseDcmsRequestObservability(this IApplicationBuilder app, string serviceName) =>
        app.Use(async (context, next) =>
        {
            var startedAt = DateTimeOffset.UtcNow;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("dCMS.Observability.Http");
            var correlationId = GetCorrelationId(context);
            var tenantId = GetTenantId(context);
            var storeId = GetStoreId(context);

            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["service"] = serviceName,
                ["correlationId"] = correlationId,
                ["tenantId"] = tenantId,
                ["storeId"] = storeId,
            });

            Exception? exception = null;
            try
            {
                await next().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exception = ex;
                context.Items[DcmsWebHostDefaults.FailureReasonItemName] = "unhandled_exception";
                throw;
            }
            finally
            {
                var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
                var route = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "unknown";
                var failureReason = ResolveFailureReason(context, exception);
                DcmsObservabilityMetrics.ObserveHttpRequest(serviceName, route, context.Response.StatusCode, failureReason);

                if (exception is not null)
                {
                    logger.LogError(
                        exception,
                        "HTTP request failed service {Service} route {Route} status {StatusCode} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} store {StoreId} elapsedMs {ElapsedMs}",
                        serviceName,
                        route,
                        context.Response.StatusCode,
                        failureReason,
                        correlationId,
                        tenantId,
                        storeId,
                        elapsedMs);
                }
                else
                {
                    var level = context.Response.StatusCode >= 500 ? LogLevel.Error :
                        context.Response.StatusCode >= 400 ? LogLevel.Warning :
                        LogLevel.Information;
                    logger.Log(
                        level,
                        "HTTP request completed service {Service} route {Route} status {StatusCode} failure {FailureReason} correlation {CorrelationId} tenant {TenantId} store {StoreId} elapsedMs {ElapsedMs}",
                        serviceName,
                        route,
                        context.Response.StatusCode,
                        failureReason,
                        correlationId,
                        tenantId,
                        storeId,
                        elapsedMs);
                }
            }
        });

    public static void SetDcmsFailureReason(this HttpContext context, string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
            context.Items[DcmsWebHostDefaults.FailureReasonItemName] = reason.Trim();
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(DcmsWebHostDefaults.CorrelationIdHeaderName, out var existing))
        {
            var raw = existing.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
                return raw.Length > 128 ? raw[..128] : raw;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(DcmsWebHostDefaults.CorrelationIdHeaderName, out var value)
            ? value?.ToString() ?? context.TraceIdentifier
            : context.TraceIdentifier;

    private static string GetTenantId(HttpContext context) =>
        context.Request.RouteValues.TryGetValue("tenantId", out var routeTenant) && routeTenant is not null
            ? routeTenant.ToString() ?? "unknown"
            : context.Request.Headers["X-Tenant-Id"].FirstOrDefault()
              ?? context.User.FindFirst("tenant_id")?.Value
              ?? "unknown";

    private static string GetStoreId(HttpContext context) =>
        context.Request.RouteValues.TryGetValue("storeId", out var routeStore) && routeStore is not null
            ? routeStore.ToString() ?? "unknown"
            : context.Request.Headers["X-Store-Id"].FirstOrDefault()
              ?? context.User.FindFirst("store_id")?.Value
              ?? "unknown";

    private static string ResolveFailureReason(HttpContext context, Exception? exception)
    {
        if (exception is not null)
            return "unhandled_exception";

        if (context.Items.TryGetValue(DcmsWebHostDefaults.FailureReasonItemName, out var reason) &&
            reason is not null &&
            !string.IsNullOrWhiteSpace(reason.ToString()))
            return reason.ToString()!;

        return context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status409Conflict => "conflict",
            StatusCodes.Status429TooManyRequests => "rate_limited",
            >= 500 => "server_error",
            >= 400 => "client_error",
            _ => "none",
        };
    }
}

public static class DcmsRateLimitingPartitionKeys
{
    public static string FromTenantHeaderOrRemoteIp(HttpContext context)
    {
        var tenant = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tenant))
            return tenant.Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static string FromTenantClaimOrRemoteIp(HttpContext context)
    {
        var tenant = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenant))
            return tenant.Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
