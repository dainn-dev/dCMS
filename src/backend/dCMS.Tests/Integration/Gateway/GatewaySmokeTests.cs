using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dCMS.Gateway;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Gateway;

/// <summary>
/// DAI-585: Gateway smoke tests.
/// Uses <see cref="WebApplicationFactory{T}"/> with Auth:Enabled=false to test routing,
/// and Auth:Enabled=true to test auth middleware behaviour.
/// Upstream services are not started — tests verify gateway routing / auth logic in isolation.
/// </summary>
public sealed class GatewaySmokeTests
{
    private const string TestSigningKey = "dcms-test-signing-key-min-32-chars-ok!!";
    private const string TestIssuer     = "dcms-gateway";
    private const string TestAudience   = "dcms-services";

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Auth disabled — test routing and health endpoints only.</summary>
    private static WebApplicationFactory<GatewayAssemblyMarker> NoAuthFactory() =>
        new WebApplicationFactory<GatewayAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:Enabled", "false");
            b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            // Point upstreams to a non-existent address — we only test routing decisions
            b.UseSetting("ReverseProxy:Clusters:catalog-cluster:Destinations:default:Address",   "http://127.0.0.1:19999");
            b.UseSetting("ReverseProxy:Clusters:orders-cluster:Destinations:default:Address",    "http://127.0.0.1:19999");
            b.UseSetting("ReverseProxy:Clusters:inventory-cluster:Destinations:default:Address", "http://127.0.0.1:19999");
        });

    /// <summary>Auth enabled — test token validation and 401/403 responses.</summary>
    private static WebApplicationFactory<GatewayAssemblyMarker> AuthFactory() =>
        new WebApplicationFactory<GatewayAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:Enabled",        "true");
            b.UseSetting("Auth:JwtSigningKey",  TestSigningKey);
            b.UseSetting("Auth:Issuer",         TestIssuer);
            b.UseSetting("Auth:Audience",       TestAudience);
            b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            b.UseSetting("ReverseProxy:Clusters:catalog-cluster:Destinations:default:Address",   "http://127.0.0.1:19999");
            b.UseSetting("ReverseProxy:Clusters:orders-cluster:Destinations:default:Address",    "http://127.0.0.1:19999");
            b.UseSetting("ReverseProxy:Clusters:inventory-cluster:Destinations:default:Address", "http://127.0.0.1:19999");
        });

    private static string MintToken(string tenantId = "t1", string role = "ChainAdmin")
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer:             TestIssuer,
            audience:           TestAudience,
            claims:
            [
                new Claim("tenant_id", tenantId),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
            ],
            expires:            DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Health ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_endpoint_returns_200_without_auth()
    {
        using var factory = NoAuthFactory();
        using var client  = factory.CreateClient();

        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task Health_endpoint_returns_200_when_auth_enabled()
    {
        using var factory = AuthFactory();
        using var client  = factory.CreateClient();

        // /health must be accessible without a token
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Auth middleware ───────────────────────────────────────────────────────

    [Fact]
    public async Task Auth_enabled_no_token_returns_401()
    {
        using var factory = AuthFactory();
        using var client  = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("unauthorized");
    }

    [Fact]
    public async Task Auth_enabled_invalid_token_returns_401()
    {
        using var factory = AuthFactory();
        using var client  = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.valid");

        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_enabled_valid_token_passes_middleware()
    {
        using var factory = AuthFactory();
        using var client  = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken());

        // Upstream is not running → expect 502 Bad Gateway (not 401/403)
        // This proves the middleware passed the request through to YARP
        var resp = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ── Routing ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/gateway/v1/catalog/tenants/t1/stores/s1/products")]
    [InlineData("/gateway/v1/orders/tenants/t1/orders")]
    [InlineData("/gateway/v1/inventory/tenants/t1/stock")]
    public async Task Known_gateway_routes_are_not_404(string path)
    {
        using var factory = NoAuthFactory();
        using var client  = factory.CreateClient();

        var resp = await client.GetAsync(path);
        // 502 = YARP reached upstream config but upstream refused (not running in test)
        // 404 would mean YARP has no route — that is the failure we're guarding against
        resp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_gateway_route_returns_404()
    {
        using var factory = NoAuthFactory();
        using var client  = factory.CreateClient();

        var resp = await client.GetAsync("/gateway/v1/unknown/some/path");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Rate limiting ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Rate_limit_exceeded_returns_429_with_correct_error_shape()
    {
        // Use a very tight limit (1 req / 60s) so we hit it on the 2nd request
        using var factory = new WebApplicationFactory<GatewayAssemblyMarker>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Auth:Enabled",               "false");
                b.UseSetting("RateLimiting:PermitLimit",   "1");
                b.UseSetting("RateLimiting:WindowSeconds", "60");
                b.UseSetting("Cors:AllowedOrigins:0",      "http://localhost");
                b.UseSetting("ReverseProxy:Clusters:catalog-cluster:Destinations:default:Address",   "http://127.0.0.1:19999");
                b.UseSetting("ReverseProxy:Clusters:orders-cluster:Destinations:default:Address",    "http://127.0.0.1:19999");
                b.UseSetting("ReverseProxy:Clusters:inventory-cluster:Destinations:default:Address", "http://127.0.0.1:19999");
            });

        using var client = factory.CreateClient();
        // Use a unique X-Forwarded-For to get an isolated partition
        var uniqueIp = $"10.0.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", uniqueIp);

        // First request — should pass (or 502 from upstream down)
        var first = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        // Second request — should be rate limited
        var second = await client.GetAsync("/gateway/v1/catalog/tenants/t1/brands");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var body = await second.Content.ReadAsStringAsync();
        body.Should().Contain("rate_limit_exceeded");
    }
}
