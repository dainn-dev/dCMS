using Dapper;
using dCMS.Catalog.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.Elasticsearch;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

/// <summary>PostgreSQL (catalog schema + category seed) + Elasticsearch + <see cref="WebApplicationFactory{TEntryPoint}"/> with <c>Auth:Enabled=false</c>.</summary>
public sealed class CatalogApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private ElasticsearchContainer? _elasticsearch;
    private WebApplicationFactory<CatalogApiAssemblyMarker>? _factory;

    public WebApplicationFactory<CatalogApiAssemblyMarker>? Factory => _factory;
    public bool IsReady { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_catalog_api_itest")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();

            _elasticsearch = new ElasticsearchBuilder().Build();

            await Task.WhenAll(_postgres.StartAsync(), _elasticsearch.StartAsync()).ConfigureAwait(false);

            var catalogConnectionString = _postgres.GetConnectionString();
            await using (var conn = new NpgsqlConnection(catalogConnectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                var baseDir = AppContext.BaseDirectory;
                var script = await File.ReadAllTextAsync(Path.Combine(baseDir, "Migrations", "001_CreateCategories.sql"))
                    .ConfigureAwait(false);
                await conn.ExecuteAsync(script).ConfigureAwait(false);

                var axesScript = await File.ReadAllTextAsync(Path.Combine(baseDir, "Migrations", "011_CreateCatalogVariantAxes.sql"))
                    .ConfigureAwait(false);
                await conn.ExecuteAsync(axesScript).ConfigureAwait(false);

                var colorAttrId = await conn.QuerySingleAsync<int>(
                    """
                    INSERT INTO "CatalogAttributes" ("TenantId", "Name", "SortOrder")
                    VALUES ('t1', 'Color', 0)
                    RETURNING "Id";
                    """).ConfigureAwait(false);
                await conn.ExecuteAsync(
                    """
                    INSERT INTO "CatalogAttributeValues" ("AttributeId", "Name", "SortOrder") VALUES
                    (@Aid, 'Red', 0),
                    (@Aid, 'Blue', 1);
                    """,
                    new { Aid = colorAttrId }).ConfigureAwait(false);

                var sizeAttrId = await conn.QuerySingleAsync<int>(
                    """
                    INSERT INTO "CatalogAttributes" ("TenantId", "Name", "SortOrder")
                    VALUES ('t1', 'Size', 1)
                    RETURNING "Id";
                    """).ConfigureAwait(false);
                await conn.ExecuteAsync(
                    """
                    INSERT INTO "CatalogAttributeValues" ("AttributeId", "Name", "SortOrder") VALUES
                    (@Aid, 'S', 0),
                    (@Aid, 'M', 1);
                    """,
                    new { Aid = sizeAttrId }).ConfigureAwait(false);

                var parentId = await conn.QuerySingleAsync<int>(
                    """
                    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
                    VALUES ('t1', NULL, '/', 0, 'Electronics', 'electronics', 0)
                    RETURNING "Id";
                    """).ConfigureAwait(false);

                await conn.ExecuteAsync(
                    """
                    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
                    VALUES ('t1', @ParentId, '/phones/', 1, 'Phones', 'phones', 0);
                    """,
                    new { ParentId = parentId }).ConfigureAwait(false);
            }

            var esUri = _elasticsearch.GetConnectionString();
            _factory = new WebApplicationFactory<CatalogApiAssemblyMarker>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Catalog", catalogConnectionString);
                builder.UseSetting("Elasticsearch:Url", esUri);
                builder.UseSetting("Auth:Enabled", "false");
                builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            });

            IsReady = true;
        }
        catch
        {
            IsReady = false;
            if (_factory is not null)
            {
                await _factory.DisposeAsync().ConfigureAwait(false);
                _factory = null;
            }

            if (_postgres is not null)
            {
                await _postgres.DisposeAsync().ConfigureAwait(false);
                _postgres = null;
            }

            if (_elasticsearch is not null)
            {
                await _elasticsearch.DisposeAsync().ConfigureAwait(false);
                _elasticsearch = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync().ConfigureAwait(false);
        _factory = null;
        if (_postgres is not null)
            await _postgres.DisposeAsync().ConfigureAwait(false);
        _postgres = null;
        if (_elasticsearch is not null)
            await _elasticsearch.DisposeAsync().ConfigureAwait(false);
        _elasticsearch = null;
    }
}
