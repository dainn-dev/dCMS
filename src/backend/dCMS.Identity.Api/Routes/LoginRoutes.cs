using dCMS.Identity.Api.Persistence;
using dCMS.Identity.Api.Security;
using Microsoft.Extensions.Options;

namespace dCMS.Identity.Api.Routes;

public static class LoginRoutes
{
    private static readonly TimeSpan LoginTokenTtl = TimeSpan.FromHours(8);

    public static void MapLoginRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/identity").WithTags("identity");

        group.MapPost("login", Login).AllowAnonymous();
        group.MapGet("me", Me).RequireAuthorization();
    }

    public sealed record LoginRequest(string Email, string Password);
    public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role, string? TenantId, string DisplayName, Guid UserId);

    private static async Task<IResult> Login(
        LoginRequest req,
        IAuthUserStore users,
        DcmsPasswordHasher hasher,
        JwtMinter minter,
        IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Bad("invalid_request", "Email and password are required.");

        var clientId = config.GetSection("Dcms:Client")["Id"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            return Bad("misconfigured", "Dcms:Client.Id is not configured.");

        var user = await users.FindByEmailAsync(clientId, req.Email.Trim(), ct);
        if (user is null || !user.IsActive)
            return Unauthorized();

        if (!hasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized();

        var minted = minter.MintLogin(user, LoginTokenTtl);
        return Ok(new LoginResponse(minted.Token, minted.ExpiresAt, user.Role, user.TenantId, user.DisplayName, user.Id));
    }

    private static IResult Me(HttpContext ctx)
    {
        var user = ctx.User;
        var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            return Unauthorized();

        return Ok(new
        {
            userId = sub,
            displayName = user.Identity?.Name,
            email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            tenantId = user.FindFirst(AspNetCore.Auth.DcmsClaims.TenantId)?.Value,
            clientId = user.FindFirst(AspNetCore.Auth.DcmsClaims.ClientId)?.Value,
            impersonatedBy = user.FindFirst(AspNetCore.Auth.DcmsClaims.ImpersonatedBy)?.Value,
            originalRole = user.FindFirst(AspNetCore.Auth.DcmsClaims.OriginalRole)?.Value,
        });
    }

    private static IResult Ok(object data) => Results.Json(new { data, meta = (object?)null, error = (object?)null });
    private static IResult Bad(string code, string message) =>
        Results.BadRequest(new { data = (object?)null, meta = (object?)null, error = new { code, message } });
    private static IResult Unauthorized() =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code = "invalid_credentials", message = "Email or password is incorrect." } }, statusCode: 401);
}
