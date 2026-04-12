using Microsoft.AspNetCore.Http;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// Ensures JWT <c>tenant_id</c> and <c>store_id</c> match route values (US-5).
/// <see cref="DcmsRoles.SuperAdmin"/> bypasses the check.
/// </summary>
public sealed class TenantStoreAccessEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var tenantRoute = http.Request.RouteValues.TryGetValue("tenantId", out var tv) ? tv?.ToString() : null;
        var storeRoute = http.Request.RouteValues.TryGetValue("storeId", out var sv) ? sv?.ToString() : null;

        if (string.IsNullOrWhiteSpace(tenantRoute) || string.IsNullOrWhiteSpace(storeRoute))
            return await next(context);

        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Json(
                new { data = (object?)null, meta = (object?)null, error = new { code = "unauthorized", message = "Authentication required." } },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.IsInRole(DcmsRoles.SuperAdmin))
            return await next(context);

        var tid = user.FindFirst(DcmsClaims.TenantId)?.Value;
        var sid = user.FindFirst(DcmsClaims.StoreId)?.Value;
        if (!string.Equals(tid, tenantRoute, StringComparison.Ordinal))
        {
            return Results.Json(
                new
                {
                    data = (object?)null,
                    meta = (object?)null,
                    error = new { code = "tenant_mismatch", message = "Token tenant does not match the request path." }
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        // US-11: ChainAdmin / BrandManager may operate across stores within the same tenant; others must match store.
        if (!user.IsInRole(DcmsRoles.ChainAdmin) && !user.IsInRole(DcmsRoles.BrandManager)
            && !string.Equals(sid, storeRoute, StringComparison.Ordinal))
        {
            return Results.Json(
                new
                {
                    data = (object?)null,
                    meta = (object?)null,
                    error = new { code = "tenant_mismatch", message = "Token store does not match the request path." }
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
