using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace dCMS.AspNetCore.Auth.Middleware;

/// <summary>
/// DAI-752 (US-5) — checks two things on every request:
///   1. If the JWT carries a <c>jti</c> that has been blacklisted (impersonation:end), return 401.
///   2. If the JWT carries <c>impersonated_by</c>, populate <c>HttpContext.Items</c> so existing
///      audit pipelines can attribute writes to both the HQ actor and the effective tenant.
///
/// The middleware does NOT itself write audit rows — every dCMS service already has an
/// <c>AuditMiddleware</c> in <c>dCMS.Infrastructure.Audit</c> that picks up these context items.
/// </summary>
public sealed class ImpersonationAuditMiddleware
{
    public const string ImpersonatedByContextKey = "Impersonation:ActorUserId";
    public const string OriginalRoleContextKey = "Impersonation:OriginalRole";
    public const string EffectiveTenantContextKey = "Impersonation:EffectiveTenantId";

    private readonly RequestDelegate _next;
    private readonly IJwtBlacklist _blacklist;
    private readonly ILogger<ImpersonationAuditMiddleware> _logger;

    public ImpersonationAuditMiddleware(
        RequestDelegate next, IJwtBlacklist blacklist, ILogger<ImpersonationAuditMiddleware> logger)
    {
        _next = next;
        _blacklist = blacklist;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var jti = user.FindFirst(DcmsClaims.Jti)?.Value
                  ?? user.FindFirst("jti")?.Value;
        if (!string.IsNullOrWhiteSpace(jti) &&
            await _blacklist.IsBlacklistedAsync(jti, context.RequestAborted).ConfigureAwait(false))
        {
            _logger.LogInformation("Rejecting blacklisted token (jti={Jti}).", jti);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new { code = "token_revoked", message = "Token has been revoked." }
            }).ConfigureAwait(false);
            return;
        }

        var impersonatedBy = user.FindFirst(DcmsClaims.ImpersonatedBy)?.Value;
        if (!string.IsNullOrWhiteSpace(impersonatedBy))
        {
            context.Items[ImpersonatedByContextKey] = impersonatedBy;
            context.Items[OriginalRoleContextKey] =
                user.FindFirst(DcmsClaims.OriginalRole)?.Value ?? DcmsRoles.ClientAdmin;
            context.Items[EffectiveTenantContextKey] =
                user.FindFirst(DcmsClaims.TenantId)?.Value ?? string.Empty;
        }

        await _next(context).ConfigureAwait(false);
    }
}

public static class ImpersonationAuditMiddlewareExtensions
{
    /// <summary>Inserts the impersonation audit + blacklist check after authentication.</summary>
    public static IApplicationBuilder UseDcmsImpersonationAudit(this IApplicationBuilder app) =>
        app.UseMiddleware<ImpersonationAuditMiddleware>();
}
