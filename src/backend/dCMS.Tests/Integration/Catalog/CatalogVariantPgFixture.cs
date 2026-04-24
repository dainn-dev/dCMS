using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

/// <summary>Shared PostgreSQL + catalog schema + seed rows for variant persistence tests (one container per collection).</summary>
public sealed class CatalogVariantPgFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString =>
        _container?.GetConnectionString() ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dcms_catalog_itest")
            .WithUsername("dcms")
            .WithPassword("test")
            .Build();
        await _container.StartAsync();

        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        var baseDir = AppContext.BaseDirectory;
        foreach (var name in new[]
                 {
                     "001_CreateCategories.sql", "003_CreateProducts.sql", "004_CreateVariants.sql",
                     "009_CreateAuditAndNotifications.sql",
                     "010_AddCombinationCanonical.sql", "012_AddVariantBasePriceAndProductImages.sql"
                 })
        {
            var path = Path.Combine(baseDir, "Migrations", name);
            var script = await File.ReadAllTextAsync(path);
            await conn.ExecuteAsync(script);
        }

        var categoryId = await conn.QuerySingleAsync<int>(
            """
            INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
            VALUES ('t1', NULL, '/', 0, 'Root', 'root', 0)
            RETURNING "Id";
            """);

        var now = DateTimeOffset.Parse("2026-04-12T12:00:00Z");
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        await conn.ExecuteAsync(
            """
            INSERT INTO "Products" ("Id", "TenantId", "StoreId", "CategoryId", "Name", "Description", "Slug", "Status", "SalesCount30d", "CreatedAt", "UpdatedAt")
            VALUES (@Pid, 't1', 's1', @CatId, '{}', '{}', 'slug-p1', 'draft', 0, @Now, @Now);
            INSERT INTO "ProductVariants" ("Id", "ProductId", "SKU", "CombinationHash", "Status", "SortOrder")
            VALUES
                ('v1', @Pid, 'SKU-A', @HashA, 'active', 0),
                ('v2', @Pid, 'SKU-B', @HashB, 'active', 1);
            """,
            new { Pid = "p1", CatId = categoryId, Now = now, HashA = hashA, HashB = hashB });
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

[CollectionDefinition("catalog-variant-pg")]
public sealed class CatalogVariantCollection : ICollectionFixture<CatalogVariantPgFixture>;
