using System.Net;
using System.Net.Http.Headers;
using dCMS.AspNetCore.Auth;
using FluentAssertions;
using dCMS.Reports.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Access;

[CollectionDefinition("ReportsApiAuth", DisableParallelization = true)]
public sealed class ReportsApiAuthCollection : ICollectionFixture<ReportsApiAuthFixture>
{
}

[Collection("ReportsApiAuth")]
public sealed class ReportsApiAuthTests(ReportsApiAuthFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    [SkippableFact]
    public async Task Sales_report_without_bearer_returns_401()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SaasCoreSeeds.TenantA);
        client.DefaultRequestHeaders.Add("X-Store-Id", SaasCoreSeeds.StoreA1);

        var response = await client.GetAsync("/api/v1/reports/sales?dateFrom=2026-01-01&dateTo=2026-01-31");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Sales_report_tenant_header_mismatch_returns_403()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SaasCoreJwtFactory.MintForRole(DcmsRoles.ChainAdmin, SaasCoreSeeds.TenantA));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SaasCoreSeeds.TenantB);
        client.DefaultRequestHeaders.Add("X-Store-Id", SaasCoreSeeds.StoreB1);

        var response = await client.GetAsync("/api/v1/reports/sales?dateFrom=2026-01-01&dateTo=2026-01-31");
        await SaasCoreHttpAssert.AssertAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [SkippableFact]
    public async Task Sales_report_matching_headers_returns_200()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SaasCoreJwtFactory.MintForRole(DcmsRoles.ChainAdmin, SaasCoreSeeds.TenantA));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SaasCoreSeeds.TenantA);
        client.DefaultRequestHeaders.Add("X-Store-Id", SaasCoreSeeds.StoreA1);

        var response = await client.GetAsync("/api/v1/reports/sales?dateFrom=2026-01-01&dateTo=2026-01-31");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public sealed class ReportsApiAuthFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<ReportsApiAssemblyMarker>? _factory;

    public WebApplicationFactory<ReportsApiAssemblyMarker>? Factory => _factory;
    public bool IsReady { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_reports_auth_it")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();

            await _postgres.StartAsync().ConfigureAwait(false);

            var sql = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Migrations", "Analytics", "001_CreateAnalyticsTables.sql"))
                .ConfigureAwait(false);

            await using (var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString()))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await Dapper.SqlMapper.ExecuteAsync(conn, sql).ConfigureAwait(false);
            }

            var analyticsCs = _postgres.GetConnectionString();
            _factory = new WebApplicationFactory<ReportsApiAssemblyMarker>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Analytics", analyticsCs);
                b.UseSetting("Auth:Enabled", "true");
                b.UseSetting("Auth:JwtSigningKey", SaasCoreSeeds.JwtKey);
                b.UseSetting("Auth:Issuer", SaasCoreSeeds.JwtIssuer);
                b.UseSetting("Auth:Audience", SaasCoreSeeds.JwtAudience);
                b.UseSetting("Dcms:Client:Id", SaasCoreSeeds.ClientId);
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
