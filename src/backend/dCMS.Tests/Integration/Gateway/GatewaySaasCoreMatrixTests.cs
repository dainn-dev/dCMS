using System.Net;
using System.Net.Http.Headers;
using dCMS.Billing.Domain;
using dCMS.Gateway;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dCMS.Tests.Integration.Gateway;

/// <summary>SaaS Core gateway edge matrix — auth, entitlement, rate limit.</summary>
public sealed class GatewaySaasCoreMatrixTests
{
    private const string TestSigningKey = "dcms-test-signing-key-min-32-chars-ok!!";
    private const string TestIssuer = "dcms-gateway";
    private const string TestAudience = "dcms-services";

    private static WebApplicationFactory<GatewayAssemblyMarker> AuthFactoryWithEntitlement(
        TenantEntitlementSnapshot? snapshot) =>
        new WebApplicationFactory<GatewayAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:Enabled", "true");
            b.UseSetting("Auth:JwtSigningKey", TestSigningKey);
            b.UseSetting("Auth:Issuer", TestIssuer);
            b.UseSetting("Auth:Audience", TestAudience);
            b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            b.UseSetting("ReverseProxy:Clusters:catalog-cluster:Destinations:default:Address", "http://127.0.0.1:19999");
            b.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(ITenantEntitlementStore)).ToList())
                    services.Remove(d);
                services.AddSingleton<ITenantEntitlementStore>(new FixedStore(snapshot));
            });
        });

    [Fact]
    public async Task No_bearer_returns_401()
    {
        using var factory = AuthFactoryWithEntitlement(ActiveSnapshot());
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t-saas-a/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_jwt_active_entitlement_not_401_or_403()
    {
        using var factory = AuthFactoryWithEntitlement(ActiveSnapshot());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GatewayTenantEntitlementMiddlewareTestsMint.Token(TestSigningKey, TestIssuer, TestAudience));

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t-saas-a/brands");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Suspended_entitlement_returns_403()
    {
        using var factory = AuthFactoryWithEntitlement(
            TenantEntitlementSnapshot.Create("t-saas-a", PlanCode.Starter, TenantSubscriptionState.Suspended,
                ManualInvoiceStatus.None, true, null, 2, 500, ["catalog.write"], 1));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GatewayTenantEntitlementMiddlewareTestsMint.Token(TestSigningKey, TestIssuer, TestAudience));

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t-saas-a/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Super_admin_bypasses_missing_entitlement()
    {
        using var factory = AuthFactoryWithEntitlement(null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                GatewayTenantEntitlementMiddlewareTestsMint.Token(TestSigningKey, TestIssuer, TestAudience, role: "SuperAdmin"));

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t-saas-a/brands");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rate_limit_exceeded_returns_429()
    {
        using var factory = new WebApplicationFactory<GatewayAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:Enabled", "false");
            b.UseSetting("RateLimiting:PermitLimit", "1");
            b.UseSetting("RateLimiting:WindowSeconds", "60");
            b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            b.UseSetting("ReverseProxy:Clusters:catalog-cluster:Destinations:default:Address", "http://127.0.0.1:19999");
            b.UseGatewayTestEntitlementStore();
        });

        using var client = factory.CreateClient();
        var ip = $"10.99.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);

        var first = await client.GetAsync("/gateway/v1/catalog/tenants/t-saas-a/brands");
        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        var second = await client.GetAsync("/gateway/v1/catalog/tenants/t-saas-a/brands");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static TenantEntitlementSnapshot ActiveSnapshot() =>
        TenantEntitlementSnapshot.Create(
            "t-saas-a", PlanCode.Starter, TenantSubscriptionState.Active,
            ManualInvoiceStatus.None, true, null, 2, 500, ["catalog.write"], 1);

    private sealed class FixedStore(TenantEntitlementSnapshot? snapshot) : ITenantEntitlementStore
    {
        public Task<TenantEntitlementSnapshot?> TryGetAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task PublishAsync(TenantEntitlementSnapshot s, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<long> BumpVersionAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
    }
}

/// <summary>Shared JWT mint helper for gateway matrix tests.</summary>
internal static class GatewayTenantEntitlementMiddlewareTestsMint
{
    internal static string Token(string key, string issuer, string audience, string tenantId = "t-saas-a", string role = "ChainAdmin")
    {
        var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(signingKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer,
            audience,
            [
                new System.Security.Claims.Claim("tenant_id", tenantId),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "gw-matrix"),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
