using Dapper;
using dCMS.Core.Exceptions;
using dCMS.Core.Services;
using dCMS.Infrastructure.Catalog;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

[Collection("catalog-variant-pg")]
public sealed class CatalogVariantPersistenceIntegrationTests(CatalogVariantPgFixture fixture)
{
    private readonly CatalogVariantPgFixture _fixture = fixture;

    [Fact]
    public async Task UpdateProductVariantAsync_updates_row_scoped_by_tenant_store()
    {
        var persistence = new SqlCatalogPersistence(_fixture.ConnectionString);
        var affected = await persistence.UpdateProductVariantAsync("v1", "p1", "t1", "s1", "SKU-A2", "inactive", 5);
        affected.Should().Be(1);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var row = await conn.QuerySingleAsync<(string Sku, string Status, int Sort)>(
            """SELECT "SKU", "Status", "SortOrder" FROM "ProductVariants" WHERE "Id" = 'v1'""");
        row.Sku.Should().Be("SKU-A2");
        row.Status.Should().Be("inactive");
        row.Sort.Should().Be(5);
    }

    [Fact]
    public async Task VariantSkuTakenByAnotherAsync_detects_other_variant_in_store()
    {
        var persistence = new SqlCatalogPersistence(_fixture.ConnectionString);
        var taken = await persistence.VariantSkuTakenByAnotherAsync("s1", "SKU-B", "v1");
        taken.Should().BeTrue();

        var freeForSame = await persistence.VariantSkuTakenByAnotherAsync("s1", "SKU-B", "v2");
        freeForSame.Should().BeFalse();
    }

    [Fact]
    public async Task ProductService_UpdateVariantAsync_throws_DuplicateVariantSkuException_when_sku_taken()
    {
        var persistence = new SqlCatalogPersistence(_fixture.ConnectionString);
        var svc = new ProductService(persistence);

        var act = async () => await svc.UpdateVariantAsync("v1", "p1", "t1", "s1", "SKU-B", null, null);

        await act.Should().ThrowAsync<DuplicateVariantSkuException>();
    }

    [Fact]
    public async Task GetBySlugAsync_returns_product_by_store_tenant_slug()
    {
        var persistence = new SqlCatalogPersistence(_fixture.ConnectionString);
        var p = await persistence.GetBySlugAsync("s1", "t1", "slug-p1");
        p.Should().NotBeNull();
        p!.Id.Should().Be("p1");
        p.Slug.Should().Be("slug-p1");
    }
}
