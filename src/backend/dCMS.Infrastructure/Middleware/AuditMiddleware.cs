using System.Security.Claims;
using dCMS.Core.Audit;
using dCMS.Infrastructure.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Middleware;

/// <summary>US-11: after handler, enqueue audit row for authenticated mutating API calls under <c>/api/v1/tenants/</c>.</summary>
public sealed class AuditMiddleware(RequestDelegate next, AuditLogChannel channel, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context).ConfigureAwait(false);
        if (!ShouldAudit(context))
            return;

        try
        {
            var entry = BuildEntry(context);
            if (entry is null)
                return;
            if (!channel.TryWrite(entry))
                logger.LogWarning("Audit queue full; dropped {Action} {EntityType}/{EntityId}.", entry.Action,
                    entry.EntityType, entry.EntityId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Audit middleware skipped.");
        }
    }

    private static bool ShouldAudit(HttpContext ctx)
    {
        var m = ctx.Request.Method;
        if (!HttpMethods.IsPost(m) && !HttpMethods.IsPut(m) && !HttpMethods.IsPatch(m) && !HttpMethods.IsDelete(m))
            return false;

        var p = ctx.Request.Path.Value ?? "";
        if (!p.StartsWith("/api/v1/", StringComparison.Ordinal))
            return false;

        // Public storefront (no /tenants/ segment)
        if (p.StartsWith("/api/v1/products", StringComparison.Ordinal) &&
            !p.Contains("/tenants/", StringComparison.Ordinal))
            return false;

        if (p.Contains("/health", StringComparison.Ordinal))
            return false;

        return ctx.User.Identity?.IsAuthenticated == true;
    }

    private static AuditLogEntry? BuildEntry(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        var tenant = ctx.Request.RouteValues.TryGetValue("tenantId", out var tv) ? tv?.ToString()?.Trim() : null;
        var store = ctx.Request.RouteValues.TryGetValue("storeId", out var sv) ? sv?.ToString()?.Trim() : null;
        tenant = string.IsNullOrEmpty(tenant) ? "unknown" : tenant;
        store = string.IsNullOrEmpty(store) ? "unknown" : store;

        var entityId = "-";
        foreach (var key in new[] { "productId", "variantId" })
        {
            if (ctx.Request.RouteValues.TryGetValue(key, out var rv) && rv is not null)
            {
                var s = rv.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    entityId = s.Trim();
                    break;
                }
            }
        }

        var action = $"{ctx.Request.Method} {ctx.Request.Path}";
        var diff = ctx.Request.QueryString.HasValue ? Truncate(ctx.Request.QueryString.Value!, 2048) : null;
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
        return new AuditLogEntry(tenant, store, sub, role, action, "http", entityId, diff, ip, DateTimeOffset.UtcNow);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
