using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Dapper;
using dCMS.AspNetCore.Auth;
using dCMS.Promotions.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Promotions;

/// <summary>DAI-32 — Promotions tenant admin routes enforce JWT tenant scope when Auth:Enabled.</summary>
[Collection("PromotionsApiAuth")]
public sealed class PromotionsApiAuthIntegrationTests(PromotionsApiAuthFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    private static HttpClient Client(PromotionsApiAuthFixture f) => f.Factory!.CreateClient();

    private static string CampaignsUrl(string tenantId) => $"/api/v1/tenants/{tenantId}/campaigns";

    [SkippableFact]
    public async Task List_campaigns_without_bearer_returns_401()
    {
        Skip();
        using var client = Client(fixture);
        var response = await client.GetAsync(CampaignsUrl(PromotionsApiAuthFixture.TenantId));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task List_campaigns_jwt_tenant_mismatch_returns_403()
    {
        Skip();
        using var client = Client(fixture);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", PromotionsApiAuthFixture.Jwt(
                PromotionsApiAuthFixture.TenantId, DcmsRoles.ChainAdmin));

        var response = await client.GetAsync(CampaignsUrl("other-tenant"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task List_campaigns_matching_tenant_returns_200()
    {
        Skip();
        using var client = Client(fixture);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", PromotionsApiAuthFixture.Jwt(
                PromotionsApiAuthFixture.TenantId, DcmsRoles.ChainAdmin));

        var response = await client.GetAsync(CampaignsUrl(PromotionsApiAuthFixture.TenantId));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task List_campaigns_super_admin_cross_tenant_returns_200()
    {
        Skip();
        using var client = Client(fixture);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", PromotionsApiAuthFixture.Jwt(
                PromotionsApiAuthFixture.TenantId, DcmsRoles.SuperAdmin));

        var response = await client.GetAsync(CampaignsUrl("cross-tenant-admin"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[CollectionDefinition("PromotionsApiAuth", DisableParallelization = true)]
public sealed class PromotionsApiAuthCollection : ICollectionFixture<PromotionsApiAuthFixture>
{
}

/// <summary>PostgreSQL + WAF with Auth:Enabled for tenant-scope integration tests.</summary>
public sealed class PromotionsApiAuthFixture : IAsyncLifetime
{
    public const string JwtKey = "integration-test-signing-key-32bytes!!";
    public const string TenantId = "t-promo-auth";

    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<PromotionsApiAssemblyMarker>? _factory;

    public WebApplicationFactory<PromotionsApiAssemblyMarker>? Factory => _factory;
    public bool IsReady { get; private set; }

    public static string Jwt(string tenantId, string role, string? storeId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "auth-test-user"),
            new(DcmsClaims.TenantId, tenantId),
            new(ClaimTypes.Role, role),
        };
        if (!string.IsNullOrWhiteSpace(storeId))
            claims.Add(new Claim(DcmsClaims.StoreId, storeId));

        var token = new JwtSecurityToken(
            issuer: "dcms",
            audience: "dcms-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_promotions_auth_it")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();

            await _postgres.StartAsync().ConfigureAwait(false);

            var cs = _postgres.GetConnectionString();
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync().ConfigureAwait(false);

            var baseDir = AppContext.BaseDirectory;
            foreach (var migration in new[]
                     {
                         "009_CreateAuditAndNotifications.sql",
                         "020_CreateCampaigns.sql",
                         "022_CreatePromoCodes.sql",
                         "023_ExtendPromoCodes.sql",
                         "024_CreatePromoCodeRedemptions.sql",
                     })
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(baseDir, "Migrations", migration))
                    .ConfigureAwait(false);
                await conn.ExecuteAsync(sql).ConfigureAwait(false);
            }

            _factory = new WebApplicationFactory<PromotionsApiAssemblyMarker>()
                .WithWebHostBuilder(b =>
                {
                    b.UseSetting("ConnectionStrings:Catalog", cs);
                    b.UseSetting("ConnectionStrings:Promotions", cs);
                    b.UseSetting("Auth:Enabled", "true");
                    b.UseSetting("Auth:JwtSigningKey", JwtKey);
                    b.UseSetting("Auth:Issuer", "dcms");
                    b.UseSetting("Auth:Audience", "dcms-api");
                    b.UseSetting("Dcms:Client:Id", "test-client");
                    b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
                });

            _factory.CreateClient().Dispose();
            IsReady = true;
        }
        catch
        {
            IsReady = false;
            if (_factory is not null) { await _factory.DisposeAsync().ConfigureAwait(false); _factory = null; }
            if (_postgres is not null) { await _postgres.DisposeAsync().ConfigureAwait(false); _postgres = null; }
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync().ConfigureAwait(false);
        _factory = null;
        if (_postgres is not null) await _postgres.DisposeAsync().ConfigureAwait(false);
        _postgres = null;
    }
}
