using Microsoft.AspNetCore.Http;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// DAI-700 (AC3) — Brand-scope filter. Validates that the JWT's tenant + brand claim
/// covers the route's <c>{brandId}</c> (or <c>?brandId=</c>) value.
/// SuperAdmin bypasses; ChainAdmin always allowed within tenant; other roles must have
/// the brand listed in <see cref="DcmsClaims.BrandIds"/>.
/// </summary>
public sealed class BrandScopeAccessEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var brand = ResolveBrand(http);
        if (string.IsNullOrWhiteSpace(brand))
            return Forbid("BRAND_SCOPE_REQUIRED", "brandId is required for this endpoint.", StatusCodes.Status400BadRequest);

        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            return Forbid("UNAUTHORIZED", "Authentication required.", StatusCodes.Status401Unauthorized);

        if (user.IsInRole(DcmsRoles.SuperAdmin))
            return await next(context);

        // Tenant gate is mandatory.
        var tid = user.FindFirst(DcmsClaims.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tid))
            return Forbid("FORBIDDEN", "Token is missing tenant_id claim.");

        if (user.IsInRole(DcmsRoles.ChainAdmin))
            return await next(context); // ChainAdmin operates across all brands within tenant.

        var allowedBrands = DcmsScopeClaimParser.ParseCsvClaim(user.FindFirst(DcmsClaims.BrandIds)?.Value);
        if (allowedBrands.Count == 0 || !allowedBrands.Contains(brand!, StringComparer.Ordinal))
            return Forbid("FORBIDDEN", "Token does not grant access to the requested brand.");

        return await next(context);
    }

    private static string? ResolveBrand(HttpContext http)
    {
        if (http.Request.RouteValues.TryGetValue("brandId", out var bv) && bv is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        var q = http.Request.Query["brandId"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(q) ? null : q;
    }

    private static IResult Forbid(string code, string message, int status = StatusCodes.Status403Forbidden) =>
        Results.Json(
            new { data = (object?)null, meta = (object?)null, error = new { code, message } },
            statusCode: status);
}
