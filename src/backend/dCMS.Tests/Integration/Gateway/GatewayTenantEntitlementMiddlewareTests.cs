using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dCMS.Billing.Domain;
using dCMS.Gateway;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Gateway;

public sealed class GatewayTenantEntitlementMiddlewareTests
{
    private const string TestSigningKey = "dcms-test-signing-key-min-32-chars-ok!!";
    private const string TestIssuer = "dcms-gateway";
    private const string TestAudience = "dcms-services";
    private const string TenantId = "t-billing";

    private static WebApplicationFactory<GatewayAssemblyMarker> FactoryWithSnapshot(
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
                var existing = services.Where(d => d.ServiceType == typeof(ITenantEntitlementStore)).ToList();
                foreach (var d in existing)
                    services.Remove(d);
                services.AddSingleton<ITenantEntitlementStore>(new FixedTenantEntitlementStore(snapshot));
            });
        });

    private static string MintToken(string tenantId = TenantId, string role = "ChainAdmin")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims:
            [
                new Claim("tenant_id", tenantId),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Active_tenant_passes_entitlement_middleware()
    {
        using var factory = FactoryWithSnapshot(OperationalSnapshot(TenantSubscriptionState.Active));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken());

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(TenantSubscriptionState.Suspended, EntitlementErrorCodes.SubscriptionSuspended)]
    [InlineData(TenantSubscriptionState.Cancelled, EntitlementErrorCodes.SubscriptionCancelled)]
    public async Task Non_operational_subscription_returns_403(
        TenantSubscriptionState state,
        string expectedCode)
    {
        using var factory = FactoryWithSnapshot(OperationalSnapshot(state));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken());

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain(expectedCode);
    }

    [Fact]
    public async Task Expired_trial_returns_403_trial_expired()
    {
        var snapshot = TenantEntitlementSnapshot.Create(
            TenantId, PlanCode.Starter, TenantSubscriptionState.Trial, ManualInvoiceStatus.None,
            tenantActive: true, DateTimeOffset.UtcNow.AddMinutes(-5), 2, 500, ["catalog.write"], 1);
        using var factory = FactoryWithSnapshot(snapshot);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken());

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain(EntitlementErrorCodes.TrialExpired);
    }

    [Fact]
    public async Task Missing_snapshot_returns_403_entitlement_unavailable()
    {
        using var factory = FactoryWithSnapshot(null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken());

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain(EntitlementErrorCodes.EntitlementUnavailable);
    }

    [Fact]
    public async Task Super_admin_bypasses_entitlement_middleware()
    {
        using var factory = FactoryWithSnapshot(null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(role: "SuperAdmin"));

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private static TenantEntitlementSnapshot OperationalSnapshot(TenantSubscriptionState state) =>
        TenantEntitlementSnapshot.Create(
            TenantId,
            PlanCode.Starter,
            state,
            ManualInvoiceStatus.None,
            tenantActive: true,
            trialEndsAt: state == TenantSubscriptionState.Trial
                ? DateTimeOffset.UtcNow.AddDays(7)
                : null,
            maxBrands: 2,
            maxActiveProducts: 500,
            ["catalog.write"],
            version: 1);

    private sealed class FixedTenantEntitlementStore(TenantEntitlementSnapshot? snapshot) : ITenantEntitlementStore
    {
        public Task<TenantEntitlementSnapshot?> TryGetAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task PublishAsync(TenantEntitlementSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<long> BumpVersionAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
    }
}
