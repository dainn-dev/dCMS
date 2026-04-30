using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using dCMS.Identity.Api.Persistence;
using dCMS.Identity.Api.Security;

namespace dCMS.Identity.Api.Routes;

/// <summary>
/// DAI-752 (US-5) — HQ ClientAdmin only. Mints a short-lived JWT scoped to a target tenant
/// and writes an <c>ImpersonationLog</c> entry. End-impersonation blacklists the jti.
/// </summary>
public static class AdminImpersonateRoutes
{
    private static readonly TimeSpan ImpersonationTtl = TimeSpan.FromMinutes(30);

    public static void MapAdminImpersonateRoutes(this WebApplication app)
    {
        var auth = app.Configuration.IsDcmsAuthEnabled();
        var group = app.MapGroup("/api/v1/admin").WithTags("admin");
        if (auth)
            group.RequireAuthorization(DcmsPolicies.ClientAdminOnly);

        group.MapPost("impersonate", Start);
        group.MapPost("impersonate:end", End);
    }

    public sealed record StartRequest(string TargetTenantId, string Reason, string? AssumedRole);
    public sealed record StartResponse(string AccessToken, DateTimeOffset ExpiresAt, string Jti, string TargetTenantId);

    private static async Task<IResult> Start(
        StartRequest req,
        HttpContext ctx,
        IAuthUserStore users,
        IImpersonationLogStore logStore,
        JwtMinter minter,
        IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TargetTenantId))
            return Bad("invalid_request", "TargetTenantId is required.");
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Trim().Length < 4)
            return Bad("invalid_request", "Reason is required (>= 4 chars).");

        var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var actorId))
            return Unauthorized();

        var actor = await users.FindByIdAsync(actorId, ct);
        if (actor is null || !actor.IsActive)
            return Unauthorized();

        // Defence-in-depth: only ClientAdmin/SuperAdmin may impersonate.
        if (!string.Equals(actor.Role, DcmsRoles.ClientAdmin, StringComparison.Ordinal)
            && !string.Equals(actor.Role, DcmsRoles.SuperAdmin, StringComparison.Ordinal))
            return Forbid();

        var clientId = config.GetSection("Dcms:Client")["Id"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            return Bad("misconfigured", "Dcms:Client.Id is not configured.");

        var assumedRole = string.IsNullOrWhiteSpace(req.AssumedRole)
            ? DcmsRoles.TenantAdmin
            : req.AssumedRole.Trim();

        var jti = Guid.NewGuid();
        var minted = minter.MintImpersonation(actor, req.TargetTenantId.Trim(), assumedRole, ImpersonationTtl, jti);

        await logStore.RecordAsync(new ImpersonationLogRow(
            Guid.NewGuid(), clientId, actor.Id, req.TargetTenantId.Trim(), req.Reason.Trim(),
            jti, DateTimeOffset.UtcNow, minted.ExpiresAt, null), ct);

        return Ok(new StartResponse(minted.Token, minted.ExpiresAt, jti.ToString(), req.TargetTenantId.Trim()));
    }

    public sealed record EndRequest(string Jti);

    private static async Task<IResult> End(
        EndRequest req,
        IImpersonationLogStore logStore,
        IJwtBlacklist blacklist,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Jti) || !Guid.TryParse(req.Jti, out var jti))
            return Bad("invalid_request", "jti is required.");

        var entry = await logStore.FindByJtiAsync(jti, ct);
        if (entry is null)
            return NotFound("not_found", "Impersonation session not found.");

        await blacklist.BlacklistAsync(jti.ToString(), entry.ExpiresAt, ct);
        await logStore.MarkEndedAsync(jti, DateTimeOffset.UtcNow, ct);

        return Ok(new { ended = true, jti = jti.ToString() });
    }

    private static IResult Ok(object data) => Results.Json(new { data, meta = (object?)null, error = (object?)null });
    private static IResult Bad(string code, string message) =>
        Results.BadRequest(new { data = (object?)null, meta = (object?)null, error = new { code, message } });
    private static IResult NotFound(string code, string message) =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code, message } }, statusCode: 404);
    private static IResult Unauthorized() =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code = "unauthorized", message = "Authentication required." } }, statusCode: 401);
    private static IResult Forbid() =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code = "forbidden", message = "Only ClientAdmin may impersonate." } }, statusCode: 403);
}
