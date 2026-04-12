using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dCMS.AspNetCore.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace dCMS.Web.CatalogProxy;

/// <summary>Issues short-lived JWTs for Catalog.Api with <see cref="DcmsClaims.TenantId"/> / <see cref="DcmsClaims.StoreId"/> + Store Manager role.</summary>
public sealed class CatalogJwtIssuer(IConfiguration configuration)
{
    public string CreateForBackOfficeUser(string subject, string tenantId, string storeId, TimeSpan? lifetime = null)
    {
        var opt = configuration.GetSection(DcmsAuthOptions.SectionName).Get<DcmsAuthOptions>() ?? new DcmsAuthOptions();
        if (string.IsNullOrWhiteSpace(opt.JwtSigningKey) || opt.JwtSigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Auth:JwtSigningKey must be set (≥32 chars) in Umbraco appsettings to match Catalog.Api token validation.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim(ClaimTypes.Role, DcmsRoles.StoreManager),
            new Claim(DcmsClaims.TenantId, tenantId),
            new Claim(DcmsClaims.StoreId, storeId)
        };
        var token = new JwtSecurityToken(opt.Issuer, opt.Audience, claims, expires: expires, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
