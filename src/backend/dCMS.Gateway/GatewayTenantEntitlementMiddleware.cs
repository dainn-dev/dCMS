using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using dCMS.Billing.Domain;
using dCMS.Infrastructure.Web;
using Microsoft.Extensions.Options;

namespace dCMS.Gateway;

/// <summary>
/// DAI-29: Hard-fail requests for inactive, suspended, cancelled, or expired-trial tenants.
/// Runs after JWT validation; reads entitlement snapshot from Redis cache.
/// </summary>
public sealed class GatewayTenantEntitlementMiddleware(
    RequestDelegate next,
    ITenantEntitlementStore entitlementStore,
    IOptions<GatewayAuthOptions> authOptions,
    ILogger<GatewayTenantEntitlementMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ITenantEntitlementStore _entitlementStore = entitlementStore;
    private readonly GatewayAuthOptions _authOptions = authOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_authOptions.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user.IsInRole(DcmsRoles.SuperAdmin))
        {
            await _next(context);
            return;
        }

        var tenantId = user.FindFirst(DcmsClaims.TenantId)?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await _next(context);
            return;
        }

        var snapshot = await _entitlementStore.TryGetAsync(tenantId, context.RequestAborted).ConfigureAwait(false);
        if (snapshot is null)
        {
            context.SetDcmsFailureReason(EntitlementErrorCodes.EntitlementUnavailable);
            logger.LogWarning(
                "Gateway entitlement check failed: snapshot unavailable for tenant {TenantId} {Method} {Path}",
                tenantId,
                context.Request.Method,
                path);
            await WriteForbidden(context, EntitlementErrorCodes.EntitlementUnavailable,
                "Tenant entitlement snapshot is unavailable.");
            return;
        }

        var reason = snapshot.ResolveOperationalReason();
        if (reason is not null)
        {
            context.SetDcmsFailureReason(reason);
            logger.LogWarning(
                "Gateway entitlement denied {Reason} for tenant {TenantId} {Method} {Path}",
                reason,
                tenantId,
                context.Request.Method,
                path);
            await WriteForbidden(context, reason, OperationalMessage(reason));
            return;
        }

        await _next(context);
    }

    private static bool IsExemptPath(string path) =>
        path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

    private static string OperationalMessage(string code) =>
        code switch
        {
            EntitlementErrorCodes.TenantInactive => "Tenant is inactive.",
            EntitlementErrorCodes.SubscriptionSuspended => "Tenant subscription is suspended.",
            EntitlementErrorCodes.SubscriptionCancelled => "Tenant subscription is cancelled.",
            EntitlementErrorCodes.TrialExpired => "Tenant trial has expired.",
            _ => "Tenant is not operational.",
        };

    private static async Task WriteForbidden(HttpContext context, string code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            data = (object?)null,
            meta = (object?)null,
            error = new { code, message },
        });
    }
}
