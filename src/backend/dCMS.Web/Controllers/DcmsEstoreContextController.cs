using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dCMS.AspNetCore.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace dCMS.Web.Controllers;

/// <summary>
/// Bootstrap endpoint for the eStore / Approval / Orders SPA sections.
/// Returns tenantId, storeId, and — when <c>Dcms:Auth:JwtSigningKey</c> is configured —
/// a short-lived Bearer token that the SPA uses when calling the dCMS Gateway.
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/estore")]
[AllowAnonymous]
public sealed class DcmsEstoreContextController : ControllerBase
{
    private static readonly JwtSecurityTokenHandler _jwtHandler = new();

    private readonly IConfiguration _configuration;

    public DcmsEstoreContextController(IConfiguration configuration)
        => _configuration = configuration;

    /// <summary>GET /umbraco/dcms/api/estore/context</summary>
    [HttpGet("context")]
    public IActionResult GetContext()
    {
        var estoreSection = _configuration.GetSection("Dcms:Estore");
        var tenantId = estoreSection["TenantId"]?.Trim();
        var storeId  = estoreSection["StoreId"]?.Trim();

        // Mint a gateway service token when auth is configured.
        // Key must match Gateway's Auth:JwtSigningKey.
        var authSection  = _configuration.GetSection("Dcms:Auth");
        var signingKey   = authSection["JwtSigningKey"]?.Trim();
        var issuer       = authSection["Issuer"]   ?? "dcms-gateway";
        var audience     = authSection["Audience"] ?? "dcms-services";
        var ttlSeconds   = int.TryParse(authSection["TokenTtlSeconds"], out var s) ? s : 3600;
        var authEnabled  = bool.TryParse(authSection["Enabled"], out var e) ? e : !string.IsNullOrWhiteSpace(signingKey);

        string? authToken = null;
        if (authEnabled && !string.IsNullOrWhiteSpace(signingKey))
        {
            authToken = MintServiceToken(
                signingKey, issuer, audience, ttlSeconds,
                tenantId, storeId);
        }

        return Ok(new
        {
            tenantId  = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
            storeId   = string.IsNullOrWhiteSpace(storeId)  ? null : storeId,
            authToken,
        });
    }

    private static string MintServiceToken(
        string signingKey, string issuer, string audience, int ttlSeconds,
        string? tenantId, string? storeId)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now         = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,  "umbraco-backoffice"),
            new(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            // Grant all dCMS roles so the backoffice token passes every downstream policy
            // (CatalogRead/Write, InventoryRead/Write, OrderAccess, etc.)
            new(ClaimTypes.Role, "ChainAdmin"),
            new(ClaimTypes.Role, "SuperAdmin"),
        };
        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim(DcmsClaims.TenantId, tenantId));
        if (!string.IsNullOrWhiteSpace(storeId))
            claims.Add(new Claim(DcmsClaims.StoreId, storeId));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          now.UtcDateTime,
            expires:            now.AddSeconds(ttlSeconds).UtcDateTime,
            signingCredentials: credentials);

        return _jwtHandler.WriteToken(token);
    }
}

