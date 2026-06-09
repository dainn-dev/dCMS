using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dCMS.AspNetCore.Auth;
using Microsoft.IdentityModel.Tokens;

namespace dCMS.Identity.Api.Security;

/// <summary>
/// Mints dCMS JWTs validated by every service via <c>DcmsJwtAuthExtensions.AddDcmsJwtAuthentication</c>.
/// HMAC-SHA256 signed with <c>Auth:JwtSigningKey</c>.
/// </summary>
public sealed class JwtMinter
{
    private readonly DcmsAuthOptions _opt;
    private readonly string _clientId;
    private readonly SigningCredentials _signing;

    public JwtMinter(DcmsAuthOptions opt, string clientId)
    {
        _opt = opt;
        _clientId = clientId;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.JwtSigningKey));
        _signing = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public sealed record MintResult(string Token, string Jti, DateTimeOffset ExpiresAt);

    public MintResult MintLogin(Persistence.AuthUserRow user, TimeSpan ttl)
    {
        var jti = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(ttl);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role),
            new(DcmsClaims.ClientId, _clientId),
        };
        if (!string.IsNullOrWhiteSpace(user.TenantId))
            claims.Add(new Claim(DcmsClaims.TenantId, user.TenantId));
        if (!string.IsNullOrWhiteSpace(user.StoreId))
            claims.Add(new Claim(DcmsClaims.StoreId, user.StoreId));

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: exp.UtcDateTime,
            signingCredentials: _signing);

        return new MintResult(new JwtSecurityTokenHandler().WriteToken(token), jti.ToString(), exp);
    }

    /// <summary>
    /// Mints an impersonation token: takes the actor's identity but stamps the target tenant + carries
    /// <c>impersonated_by</c>/<c>original_role</c> for audit downstream.
    /// </summary>
    public MintResult MintImpersonation(
        Persistence.AuthUserRow actor,
        string targetTenantId,
        string assumedRole,
        TimeSpan ttl,
        Guid jti)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(ttl);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, actor.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti.ToString()),
            new(JwtRegisteredClaimNames.Email, actor.Email),
            new(ClaimTypes.NameIdentifier, actor.Id.ToString()),
            new(ClaimTypes.Name, actor.DisplayName),
            new(ClaimTypes.Role, assumedRole),
            new(DcmsClaims.ClientId, _clientId),
            new(DcmsClaims.TenantId, targetTenantId),
            new(DcmsClaims.ImpersonatedBy, actor.Id.ToString()),
            new(DcmsClaims.OriginalRole, actor.Role),
        };

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: exp.UtcDateTime,
            signingCredentials: _signing);

        return new MintResult(new JwtSecurityTokenHandler().WriteToken(token), jti.ToString(), exp);
    }
}
