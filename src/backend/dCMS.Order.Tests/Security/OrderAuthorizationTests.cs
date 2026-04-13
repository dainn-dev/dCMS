using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using dCMS.Order.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Tests.Security;

public sealed class OrderAuthorizationTests
{
    private static IConfiguration Config(bool authEnabled) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Enabled"] = authEnabled ? "true" : "false",
            ["Auth:JwtSigningKey"] = "unit-test-signing-key-32chars-minimum!!",
        }!).Build();

    private static HttpContext HttpWithUser(params Claim[] claims)
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));
        return http;
    }

    [Fact]
    public void ValidateTenantStoreHeaders_when_auth_disabled_returns_null()
    {
        var http = HttpWithUser();
        var r = OrderAuthorization.ValidateTenantStoreHeaders(http, Config(false), "t1", "s1");
        Assert.Null(r);
    }

    [Fact]
    public void ValidateTenantStoreHeaders_staff_mismatched_store_returns_403()
    {
        var http = HttpWithUser(
            new Claim(ClaimTypes.NameIdentifier, "u1"),
            new Claim(DcmsClaims.TenantId, "t1"),
            new Claim(DcmsClaims.StoreId, "s-expected"),
            new Claim(ClaimTypes.Role, DcmsRoles.StoreStaff));

        var r = OrderAuthorization.ValidateTenantStoreHeaders(http, Config(true), "t1", "s-other");
        Assert.NotNull(r);
    }

    [Fact]
    public void ValidateCustomerOwnsOrder_blocks_cross_customer()
    {
        var http = HttpWithUser(
            new Claim(ClaimTypes.NameIdentifier, "cust-a"),
            new Claim(DcmsClaims.TenantId, "t1"),
            new Claim(DcmsClaims.StoreId, "s1"),
            new Claim(ClaimTypes.Role, DcmsRoles.Customer));

        var r = OrderAuthorization.ValidateCustomerOwnsOrder(http, Config(true), "cust-b");
        Assert.NotNull(r);
    }

    [Fact]
    public void ValidateCustomerOwnsOrder_allows_staff_without_customer_check()
    {
        var http = HttpWithUser(
            new Claim(ClaimTypes.NameIdentifier, "staff-1"),
            new Claim(DcmsClaims.TenantId, "t1"),
            new Claim(DcmsClaims.StoreId, "s1"),
            new Claim(ClaimTypes.Role, DcmsRoles.StoreManager));

        var r = OrderAuthorization.ValidateCustomerOwnsOrder(http, Config(true), "cust-b");
        Assert.Null(r);
    }

    [Fact]
    public void EffectiveListCustomerId_non_staff_forces_sub()
    {
        var http = HttpWithUser(
            new Claim(ClaimTypes.NameIdentifier, "cust-x"),
            new Claim(ClaimTypes.Role, DcmsRoles.Customer));

        var id = OrderAuthorization.EffectiveListCustomerId(http, Config(true), "someone-else");
        Assert.Equal("cust-x", id);
    }
}
