using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace dCMS.Gateway;

/// <summary>
/// DAI-581: Validates the incoming token (Bearer JWT) and mints a short-lived
/// internal dCMS JWT that is forwarded to upstream services via the
/// <c>Authorization</c> header — replacing the original token.
///
/// Flow:
///   1. Extract Bearer token from Authorization header
///   2. Validate it against the configured signing key (same key used by all dCMS services)
///   3. Extract claims (tenantId, storeId, userId, roles)
///   4. Mint a new short-lived internal JWT (TTL = InternalTokenTtlSeconds)
///   5. Overwrite Authorization header before YARP forwards the request
///
/// When <c>Auth:Enabled = false</c> (dev / tests) the middleware is a no-op.
/// </summary>
public sealed class GatewayAuthMiddleware(
    RequestDelegate next,
    IOptions<GatewayAuthOptions> options,
    ILogger<GatewayAuthMiddleware> logger)
{
    private static readonly JwtSecurityTokenHandler _handler = new();

    private readonly GatewayAuthOptions _opt   = options.Value;
    private readonly RequestDelegate    _next  = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_opt.Enabled)
        {
            await _next(context);
            return;
        }

        // /health and /metrics are unauthenticated — skip
        var path = context.Request.Path.Value ?? "";
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = ExtractBearerToken(context.Request);
        if (token is null)
        {
            await WriteUnauthorized(context, "Bearer token required.");
            return;
        }

        ClaimsPrincipal principal;
        try
        {
            principal = ValidateToken(token);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Gateway token validation failed: {Message}", ex.Message);
            await WriteUnauthorized(context, "Invalid or expired token.");
            return;
        }

        // Mint internal JWT and overwrite Authorization header
        var internalJwt = MintInternalToken(principal);
        context.Request.Headers["Authorization"] = $"Bearer {internalJwt}";

        // Propagate identity downstream (for logging / rate limiting)
        context.User = principal;

        await _next(context);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string? ExtractBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        return header["Bearer ".Length..].Trim();
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.JwtSigningKey));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = _opt.Issuer,
            ValidateAudience         = true,
            ValidAudience            = _opt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = key,
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(2),
            NameClaimType            = ClaimTypes.NameIdentifier,
            RoleClaimType            = ClaimTypes.Role,
        };

        return _handler.ValidateToken(token, parameters, out _);
    }

    private string MintInternalToken(ClaimsPrincipal principal)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now         = DateTimeOffset.UtcNow;

        // Forward all original claims verbatim — upstream services do their own RBAC
        var claims = principal.Claims.ToList();

        var token = new JwtSecurityToken(
            issuer:             _opt.Issuer,
            audience:           _opt.Audience,
            claims:             claims,
            notBefore:          now.UtcDateTime,
            expires:            now.AddSeconds(_opt.InternalTokenTtlSeconds).UtcDateTime,
            signingCredentials: credentials);

        return _handler.WriteToken(token);
    }

    private static async Task WriteUnauthorized(HttpContext context, string message)
    {
        context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            data  = (object?)null,
            meta  = (object?)null,
            error = new { code = "unauthorized", message },
        });
    }
}
