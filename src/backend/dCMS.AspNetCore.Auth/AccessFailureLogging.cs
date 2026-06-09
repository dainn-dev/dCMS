using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace dCMS.AspNetCore.Auth;

internal static class AccessFailureLogging
{
    private const string FailureReasonItemName = "dcms.failure_reason";

    public static void MarkAndLog(HttpContext http, string reason, string message)
    {
        http.Items[FailureReasonItemName] = reason;

        var logger = http.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("dCMS.Auth.Access");
        logger?.LogWarning(
            "Auth access rejected reason {FailureReason} message {Message} path {Path} correlation {CorrelationId} tenant {TenantId} store {StoreId} tokenTenant {TokenTenant} tokenStore {TokenStore}",
            reason,
            message,
            http.Request.Path.Value ?? "unknown",
            ResolveCorrelationId(http),
            http.Request.RouteValues.TryGetValue("tenantId", out var tenantRoute) ? tenantRoute?.ToString() ?? "unknown" : http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "unknown",
            http.Request.RouteValues.TryGetValue("storeId", out var storeRoute) ? storeRoute?.ToString() ?? "unknown" : http.Request.Headers["X-Store-Id"].FirstOrDefault() ?? "unknown",
            http.User.FindFirst(DcmsClaims.TenantId)?.Value ?? "missing",
            http.User.FindFirst(DcmsClaims.StoreId)?.Value ?? "missing");
    }

    private static string ResolveCorrelationId(HttpContext http) =>
        http.Items.TryGetValue("X-Correlation-Id", out var value)
            ? value?.ToString() ?? http.TraceIdentifier
            : http.TraceIdentifier;
}
