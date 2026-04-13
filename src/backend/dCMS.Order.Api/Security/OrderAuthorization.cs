using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Api.Security;

/// <summary>US-21 / DAI-328 — customer vs staff scope for Order API.</summary>
public static class OrderAuthorization
{
    public static bool IsSuperAdmin(ClaimsPrincipal user) => user.IsInRole(DcmsRoles.SuperAdmin);

    public static bool IsStaff(ClaimsPrincipal user) =>
        user.IsInRole(DcmsRoles.StoreStaff) ||
        user.IsInRole(DcmsRoles.StoreManager) ||
        user.IsInRole(DcmsRoles.BrandManager) ||
        user.IsInRole(DcmsRoles.ChainAdmin) ||
        user.IsInRole(DcmsRoles.SuperAdmin);

    /// <summary>Non-staff authenticated principal (includes explicit <see cref="DcmsRoles.Customer"/> or default B2C).</summary>
    public static bool IsCustomerOnly(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && !IsStaff(user);

    public static IResult? ValidateTenantStoreHeaders(
        HttpContext http,
        IConfiguration configuration,
        string headerTenantId,
        string headerStoreId)
    {
        if (!configuration.IsDcmsAuthEnabled())
            return null;

        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            return null;

        if (IsSuperAdmin(user))
            return null;

        var jwtTenant = user.FindFirst(DcmsClaims.TenantId)?.Value;
        var jwtStore = user.FindFirst(DcmsClaims.StoreId)?.Value;
        if (string.IsNullOrWhiteSpace(jwtTenant))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "FORBIDDEN",
                        message = "Token is missing tenant_id claim.",
                    },
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(jwtTenant, headerTenantId, StringComparison.Ordinal))
        {
            return Results.Json(
                new {
                    error = new
                    {
                        code = "FORBIDDEN",
                        message = "Token tenant does not match X-Tenant-Id.",
                    },
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (user.IsInRole(DcmsRoles.ChainAdmin) || user.IsInRole(DcmsRoles.BrandManager))
            return null;

        if (string.IsNullOrWhiteSpace(jwtStore))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "FORBIDDEN",
                        message = "Token is missing store_id claim.",
                    },
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(jwtStore, headerStoreId, StringComparison.Ordinal))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "FORBIDDEN",
                        message = "Token store does not match X-Store-Id.",
                    },
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    public static IResult? ValidateCustomerOwnsOrder(
        HttpContext http,
        IConfiguration configuration,
        string orderCustomerId)
    {
        if (!configuration.IsDcmsAuthEnabled())
            return null;

        if (IsStaff(http.User))
            return null;

        var sub = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub) ||
            !string.Equals(sub, orderCustomerId, StringComparison.Ordinal))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Not allowed to access this order." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    public static IResult? ValidateCreateOrderCustomer(
        HttpContext http,
        IConfiguration configuration,
        string bodyCustomerId)
    {
        if (!configuration.IsDcmsAuthEnabled())
            return null;

        if (IsStaff(http.User))
            return null;

        var sub = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub) ||
            !string.Equals(sub, bodyCustomerId, StringComparison.Ordinal))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "FORBIDDEN",
                        message = "customerId must match the authenticated subject.",
                    },
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    /// <summary>For non-staff, list is always scoped to JWT <c>sub</c> (ignores spoofed query).</summary>
    public static string? EffectiveListCustomerId(
        HttpContext http,
        IConfiguration configuration,
        string? queryCustomerId)
    {
        if (!configuration.IsDcmsAuthEnabled())
            return queryCustomerId;

        return IsStaff(http.User) ? queryCustomerId : http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
