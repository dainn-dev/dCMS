using Dapper;
using dCMS.Promotions.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Promotions;

/// <summary>
/// DAI-610: PostgreSQL + WAF for dCMS.Promotions.Api integration tests.
/// Runs migrations 020 (Campaigns) — same DB schema as Catalog but no ES required.
/// </summary>
public sealed class PromotionsApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<PromotionsApiAssemblyMarker>? _factory;

    public WebApplicationFactory<PromotionsApiAssemblyMarker>? Factory => _factory;
    public bool IsReady { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_promotions_itest")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();

            await _postgres.StartAsync().ConfigureAwait(false);

            var cs = _postgres.GetConnectionString();
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync().ConfigureAwait(false);

            var baseDir = AppContext.BaseDirectory;

            // Run migrations needed for Promotions.Api
            foreach (var migration in new[]
            {
                "009_CreateAuditAndNotifications.sql",
                "020_CreateCampaigns.sql",
            })
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(baseDir, "Migrations", migration))
                    .ConfigureAwait(false);
                await conn.ExecuteAsync(sql).ConfigureAwait(false);
            }

            _factory = new WebApplicationFactory<PromotionsApiAssemblyMarker>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Catalog", cs);
                b.UseSetting("Auth:Enabled", "false");
                b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            });

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
