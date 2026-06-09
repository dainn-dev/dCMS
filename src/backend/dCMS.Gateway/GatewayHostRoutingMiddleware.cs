using dCMS.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace dCMS.Gateway;

/// <summary>DAI-52-P0-01: resolve custom domain → tenant/store at the gateway edge for storefront routes.</summary>
public sealed class GatewayHostRoutingMiddleware(
    RequestDelegate next,
    IHostTenantResolver resolver,
    IOptions<GatewayHostRoutingOptions> options,
    ILogger<GatewayHostRoutingMiddleware> logger)
{
    public const string TenantHeader = "X-Tenant-Id";
    public const string StoreHeader = "X-Store-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var opt = options.Value;
        if (!opt.Enabled)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/storefront/v1", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var host = context.Request.Host.Host;
        if (IsPlatformHost(host, opt.PlatformHosts))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var resolution = await resolver.ResolveAsync(host, context.RequestAborted).ConfigureAwait(false);
        if (resolution is null)
        {
            if (opt.FailClosedOnStorefront)
            {
                logger.LogWarning(
                    "Gateway host routing: unknown host {Host} for storefront path {Path}",
                    host,
                    path);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    data = (object?)null,
                    meta = (object?)null,
                    error = new { code = "unknown_host", message = "No tenant is bound to this host." },
                }).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        context.Request.Headers[TenantHeader] = resolution.TenantId;
        context.Request.Headers[StoreHeader] = resolution.StoreId;
        context.Items["dcms.ResolvedTenantId"] = resolution.TenantId;
        context.Items["dcms.ResolvedStoreId"] = resolution.StoreId;

        await next(context).ConfigureAwait(false);
    }

    private static bool IsPlatformHost(string host, string[] platformHosts) =>
        platformHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
}
