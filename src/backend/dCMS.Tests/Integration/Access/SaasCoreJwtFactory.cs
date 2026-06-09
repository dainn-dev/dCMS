using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dCMS.AspNetCore.Auth;
using Microsoft.IdentityModel.Tokens;

namespace dCMS.Tests.Integration.Access;

public static class SaasCoreJwtFactory
{
    public static string Mint(
        string tenantId,
        string role,
        string? storeId = null,
        IEnumerable<string>? storeIds = null,
        IEnumerable<string>? brandIds = null,
        string? subject = "saas-rbac-test-user",
        string? clientId = SaasCoreSeeds.ClientId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SaasCoreSeeds.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject ?? "saas-rbac-test-user"),
            new(DcmsClaims.TenantId, tenantId),
            new(ClaimTypes.Role, role),
        };

        if (!string.IsNullOrWhiteSpace(clientId))
            claims.Add(new Claim(DcmsClaims.ClientId, clientId));

        if (!string.IsNullOrWhiteSpace(storeId))
            claims.Add(new Claim(DcmsClaims.StoreId, storeId));

#pragma warning disable CS0618
        if (storeIds is not null)
        {
            var csv = string.Join(",", storeIds.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (csv.Length > 0)
                claims.Add(new Claim(DcmsClaims.StoreIds, csv));
        }

        if (brandIds is not null)
        {
            var csv = string.Join(",", brandIds.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (csv.Length > 0)
                claims.Add(new Claim(DcmsClaims.BrandIds, csv));
        }
#pragma warning restore CS0618

        var token = new JwtSecurityToken(
            issuer: SaasCoreSeeds.JwtIssuer,
            audience: SaasCoreSeeds.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string MintForRole(string role, string? tenantId = SaasCoreSeeds.TenantA, string? storeId = SaasCoreSeeds.StoreA1) =>
        role switch
        {
            DcmsRoles.Customer => Mint(tenantId!, role, storeId),
            DcmsRoles.StoreStaff => Mint(tenantId!, role, storeId),
            DcmsRoles.StoreManager => Mint(tenantId!, role, storeId),
            DcmsRoles.BrandManager => Mint(tenantId!, role, storeId: null),
            DcmsRoles.ChainAdmin => Mint(tenantId!, role, storeId: null),
            DcmsRoles.TenantAdmin => Mint(tenantId!, role, storeId: null),
            DcmsRoles.SuperAdmin => Mint(tenantId!, role, storeId: null),
            _ => Mint(tenantId!, role, storeId),
        };
}
