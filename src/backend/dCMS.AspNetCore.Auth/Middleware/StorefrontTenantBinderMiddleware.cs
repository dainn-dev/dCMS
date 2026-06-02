using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace dCMS.AspNetCore.Auth.Middleware;

/// <summary>
/// DAI-751 / US-4: Resolves the active tenant for storefront requests from the
/// <c>X-Active-Tenant</c> header. Validates the tenant exists as an active branch
/// (via <see cref="IStorefrontTenantValidator"/>) and exposes the resolved tenant via
/// <c>HttpContext.Items["TenantId"]</c>.
///
/// Scope: only applied to <c>/api/v1/storefront/</c> routes. The bootstrap path
/// <c>/api/v1/storefront/branches</c> is exempt — that endpoint is what the SPA calls
/// *before* picking a tenant.
///
/// Failure modes:
///   400 missing_active_tenant — header absent or empty.
///   403 invalid_active_tenant — tenant doesn't belong to the current client (or inactive).
///
/// A <c>Vary: X-Active-Tenant</c> response header is appended on scoped requests so
/// downstream caches don't collapse responses across tenants.
/// </summary>
public sealed class StorefrontTenantBinderMiddleware
{
    public const string HeaderName = "X-Active-Tenant";
    public const string ContextKey = "TenantId";
    private const string ScopedPrefix = "/api/v1/storefront/";
    private const string BootstrapPrefix = "/api/v1/storefront/branches";

    private readonly RequestDelegate _next;

    public StorefrontTenantBinderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IStorefrontTenantValidator validator)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith(ScopedPrefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(BootstrapPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Response.Headers.Append("Vary", HeaderName);

        var raw = context.Request.Headers[HeaderName].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest,
                "missing_active_tenant",
                $"Header '{HeaderName}' is required for storefront requests after branch selection.")
                .ConfigureAwait(false);
            return;
        }

        if (!await validator.IsAllowedAsync(raw, context.RequestAborted).ConfigureAwait(false))
        {
            await WriteJsonAsync(context, StatusCodes.Status403Forbidden,
                "invalid_active_tenant",
                $"Tenant '{raw}' is not an active branch under the current client.")
                .ConfigureAwait(false);
            return;
        }

        context.Items[ContextKey] = raw;
        await _next(context).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            data = (object?)null,
            meta = (object?)null,
            error = new { code, message }
        }).ConfigureAwait(false);
    }
}

public static class StorefrontTenantBinderApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the storefront tenant binder middleware. Place after <c>UseRateLimiter()</c>
    /// and before route-specific middleware (e.g. idempotency).
    /// </summary>
    public static IApplicationBuilder UseDcmsStorefrontTenantBinder(this IApplicationBuilder app)
        => app.UseMiddleware<StorefrontTenantBinderMiddleware>();
}
