using dCMS.Catalog.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Elasticsearch;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace dCMS.Tests.Integration.Access;

/// <summary>Catalog API WAF with Auth:Enabled for SaaS Core RBAC tests.</summary>
[CollectionDefinition("CatalogApiAuth", DisableParallelization = true)]
public sealed class CatalogApiAuthCollection : ICollectionFixture<CatalogApiAuthFixture>
{
}

public sealed class CatalogApiAuthFixture : IAsyncLifetime
{
    public const string InternalApiKey = "catalog-internal-test-key-32chars!!";

    private PostgreSqlContainer? _postgres;
    private ElasticsearchContainer? _elasticsearch;
    private RabbitMqContainer? _rabbit;
    private WebApplicationFactory<CatalogApiAssemblyMarker>? _factory;

    public WebApplicationFactory<CatalogApiAssemblyMarker>? Factory => _factory;
    public bool IsReady { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_catalog_auth_it")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();
            _elasticsearch = new ElasticsearchBuilder().Build();
            _rabbit = new RabbitMqBuilder().Build();

            await Task.WhenAll(
                _postgres.StartAsync(),
                _elasticsearch.StartAsync(),
                _rabbit.StartAsync()).ConfigureAwait(false);

            var catalogCs = _postgres.GetConnectionString();
            var esUri = _elasticsearch.GetConnectionString();
            var rabbitPort = _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

            _factory = new WebApplicationFactory<CatalogApiAssemblyMarker>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Catalog", catalogCs);
                b.UseSetting("Elasticsearch:Url", esUri);
                b.UseSetting("Auth:Enabled", "true");
                b.UseSetting("Auth:JwtSigningKey", SaasCoreSeeds.JwtKey);
                b.UseSetting("Auth:Issuer", SaasCoreSeeds.JwtIssuer);
                b.UseSetting("Auth:Audience", SaasCoreSeeds.JwtAudience);
                b.UseSetting("Dcms:Client:Id", SaasCoreSeeds.ClientId);
                b.UseSetting("InternalCatalog:ApiKey", InternalApiKey);
                b.UseSetting("RabbitMq:Host", "127.0.0.1");
                b.UseSetting("RabbitMq:Port", rabbitPort.ToString());
                b.UseSetting("RabbitMq:User", RabbitMqBuilder.DefaultUsername);
                b.UseSetting("RabbitMq:Pass", RabbitMqBuilder.DefaultPassword);
                b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            });

            _factory.CreateClient().Dispose();
            IsReady = true;
        }
        catch
        {
            IsReady = false;
            if (_factory is not null) { await _factory.DisposeAsync().ConfigureAwait(false); _factory = null; }
            if (_rabbit is not null) { await _rabbit.DisposeAsync().ConfigureAwait(false); _rabbit = null; }
            if (_elasticsearch is not null) { await _elasticsearch.DisposeAsync().ConfigureAwait(false); _elasticsearch = null; }
            if (_postgres is not null) { await _postgres.DisposeAsync().ConfigureAwait(false); _postgres = null; }
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync().ConfigureAwait(false);
        _factory = null;
        if (_rabbit is not null) await _rabbit.DisposeAsync().ConfigureAwait(false);
        _rabbit = null;
        if (_elasticsearch is not null) await _elasticsearch.DisposeAsync().ConfigureAwait(false);
        _elasticsearch = null;
        if (_postgres is not null) await _postgres.DisposeAsync().ConfigureAwait(false);
        _postgres = null;
    }
}
