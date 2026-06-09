using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using dCMS.AspNetCore.Auth;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>SaaS Core — Order DLQ admin routes enforce SuperAdmin-only policy.</summary>
[Collection("OrderApiAuth")]
public sealed class OrderDlqAdminAuthTests(OrderApiAuthFixture fx)
{
    private const string DlqUrl = "/api/v1/admin/orders/dlq?includeDiscarded=false";

    [Fact]
    public async Task List_dlq_without_bearer_returns_401()
    {
        var client = fx.Factory.CreateClient();
        var response = await client.GetAsync(DlqUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_dlq_chain_admin_returns_403()
    {
        var client = fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt(DcmsRoles.ChainAdmin));

        var response = await client.GetAsync(DlqUrl);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_dlq_super_admin_returns_200()
    {
        var client = fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt(DcmsRoles.SuperAdmin));

        var response = await client.GetAsync(DlqUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Jwt(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OrderApiAuthFixture.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "dlq-test"),
            new(DcmsClaims.TenantId, OrderApiAuthFixture.TenantId),
            new(ClaimTypes.Role, role),
            new(DcmsClaims.ClientId, OrderApiAuthFixture.ClientId),
        };
        var token = new JwtSecurityToken(
            issuer: "dcms",
            audience: "dcms-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
