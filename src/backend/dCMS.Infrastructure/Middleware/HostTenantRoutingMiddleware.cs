using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Middleware;

/// <summary>US-11: optional domain → tenant/store hints from Redis <c>dcms:host:{hostname}</c> (value <c>tenantId|storeId</c>).</summary>
public sealed class HostTenantRoutingMiddleware(RequestDelegate next, IServiceProvider services)
{
    public const string ResolvedTenantIdItemKey = "dcms.ResolvedTenantId";
    public const string ResolvedStoreIdItemKey = "dcms.ResolvedStoreId";

    public async Task InvokeAsync(HttpContext context)
    {
        var redis = services.GetService<IConnectionMultiplexer>();
        if (redis is not null)
        {
            var host = context.Request.Host.Host;
            if (!string.IsNullOrWhiteSpace(host))
            {
                try
                {
                    var raw = await redis.GetDatabase().StringGetAsync("dcms:host:" + host.ToLowerInvariant())
                        .ConfigureAwait(false);
                    if (raw.HasValue)
                    {
                        var parts = raw.ToString().Split('|', 2, StringSplitOptions.TrimEntries);
                        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
                        {
                            context.Items[ResolvedTenantIdItemKey] = parts[0];
                            context.Items[ResolvedStoreIdItemKey] = parts[1];
                        }
                    }
                }
                catch
                {
                    /* ignore resolution failures */
                }
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
