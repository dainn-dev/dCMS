using Microsoft.AspNetCore.Http;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// Ensures JWT <c>tenant_id</c> and <c>store_id</c> match <c>X-Tenant-Id</c> and <c>X-Store-Id</c> headers (US-21 / DAI-328).
/// <see cref="DcmsRoles.SuperAdmin"/> bypasses the check.
/// ChainAdmin/BrandManager are scoped to tenant only (store mismatch allowed).
/// </summary>
public sealed class TenantStoreHeaderAccessEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var tenantHeader = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim();
        var storeHeader = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim();

        // Order API requires headers, but keep this filter generic: if missing, let the handler return 400.
        if (string.IsNullOrWhiteSpace(tenantHeader) || string.IsNullOrWhiteSpace(storeHeader))
            return await next(context);

        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Json(
                new { error = new { code = "UNAUTHORIZED", message = "Authentication required." } },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.IsInRole(DcmsRoles.SuperAdmin))
            return await next(context);

        var tid = user.FindFirst(DcmsClaims.TenantId)?.Value;
        var sid = user.FindFirst(DcmsClaims.StoreId)?.Value;
        var allowedStores = DcmsScopeClaimParser.ParseCsvClaim(user.FindFirst(DcmsClaims.StoreIds)?.Value);
        if (string.IsNullOrWhiteSpace(tid))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Token is missing tenant_id claim." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(tid, tenantHeader, StringComparison.Ordinal))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Token tenant does not match X-Tenant-Id." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (user.IsInRole(DcmsRoles.ChainAdmin) || user.IsInRole(DcmsRoles.BrandManager))
        {
            // If token explicitly constrains store scope, enforce it; else allow all stores within tenant (backward compat).
            if (allowedStores.Count > 0 && !allowedStores.Contains(storeHeader!, StringComparer.Ordinal))
            {
                return Results.Json(
                    new { error = new { code = "FORBIDDEN", message = "Token does not grant access to X-Store-Id." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }
            return await next(context);
        }

        if (string.IsNullOrWhiteSpace(sid))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Token is missing store_id claim." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        // If token explicitly constrains store scope, enforce it for store-scoped roles too.
        if (allowedStores.Count > 0 && !allowedStores.Contains(storeHeader!, StringComparer.Ordinal))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Token does not grant access to X-Store-Id." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(sid, storeHeader, StringComparison.Ordinal))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Token store does not match X-Store-Id." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
