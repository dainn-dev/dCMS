using Microsoft.AspNetCore.Http;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// Ensures the JWT <c>tenant_id</c> claim matches the <c>{tenantId}</c> route value.
/// Used for tenant-level endpoints that have no store scope (e.g. Brand API).
/// <see cref="DcmsRoles.SuperAdmin"/> bypasses the check.
/// </summary>
public sealed class TenantOnlyAccessEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var tenantRoute = http.Request.RouteValues.TryGetValue("tenantId", out var tv) ? tv?.ToString() : null;

        if (string.IsNullOrWhiteSpace(tenantRoute))
            return await next(context);

        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            return Results.Json(
                new { data = (object?)null, meta = (object?)null, error = new { code = "unauthorized", message = "Authentication required." } },
                statusCode: StatusCodes.Status401Unauthorized);

        if (user.IsInRole(DcmsRoles.SuperAdmin))
            return await next(context);

        var tid = user.FindFirst(DcmsClaims.TenantId)?.Value;
        if (!string.Equals(tid, tenantRoute, StringComparison.Ordinal))
            return Results.Json(
                new { data = (object?)null, meta = (object?)null, error = new { code = "tenant_mismatch", message = "Token tenant does not match the request path." } },
                statusCode: StatusCodes.Status403Forbidden);

        return await next(context);
    }
}
