using Dapper;
using dCMS.Core.Persistence;
using dCMS.Infrastructure.Catalog;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

[Collection("catalog-variant-pg")]
public sealed class PendingApprovalsPersistenceIntegrationTests(CatalogVariantPgFixture fixture)
{
    private readonly CatalogVariantPgFixture _fixture = fixture;

    [Fact]
    public async Task ListPendingApprovalsForStoreAsync_returns_submitter_category_and_total()
    {
        const string storeId = "s658_fields";
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var catId = await conn.QuerySingleAsync<int>("""SELECT "Id" FROM "Categories" WHERE "TenantId" = 't1' LIMIT 1""");
        var now = DateTimeOffset.Parse("2026-05-01T10:00:00Z");

        await conn.ExecuteAsync(
            """
            INSERT INTO "Products" ("Id", "TenantId", "StoreId", "CategoryId", "Name", "Description", "Slug", "Status", "SalesCount30d", "CreatedAt", "UpdatedAt")
            VALUES ('pa_pending', 't1', @StoreId, @CatId, '{"vi":"Chờ duyệt"}', '{}', 'slug-pending', 'pending_approval', 0, @Now, @Now);
            INSERT INTO "ApprovalComments" ("ProductId", "UserId", "Role", "Message", "Type", "CreatedAt")
            VALUES ('pa_pending', 'submitter-1', 'store_manager', 'Submitted for approval.', 'submitted', @Now);
            """,
            new { StoreId = storeId, CatId = catId, Now = now });

        var persistence = new SqlCatalogPersistence(_fixture.ConnectionString);
        var (items, total, next) = await persistence.ListPendingApprovalsForStoreAsync("t1", storeId, 50, null);

        total.Should().BeGreaterThanOrEqualTo(1);
        next.Should().BeNull();
        var row = items.Should().ContainSingle(x => x.Id == "pa_pending").Subject;
        row.SubmittedByUserId.Should().Be("submitter-1");
        row.SubmittedAt.Should().Be(now);
        row.CategoryPath.Should().Be("Root");
        row.Status.Should().Be("pending_approval");
        row.Name.Should().Contain("Chờ duyệt");
    }

    [Fact]
    public async Task ListPendingApprovalsForStoreAsync_cursor_pages_by_product_id()
    {
        const string storeId = "s658_cursor";
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var catId = await conn.QuerySingleAsync<int>("""SELECT "Id" FROM "Categories" WHERE "TenantId" = 't1' LIMIT 1""");
        var now = DateTimeOffset.Parse("2026-05-02T10:00:00Z");

        await conn.ExecuteAsync(
            """
            INSERT INTO "Products" ("Id", "TenantId", "StoreId", "CategoryId", "Name", "Description", "Slug", "Status", "SalesCount30d", "CreatedAt", "UpdatedAt")
            VALUES
              ('pa_b', 't1', @StoreId, @CatId, '{}', '{}', 'slug-b', 'pending_approval', 0, @Now, @Now),
              ('pa_c', 't1', @StoreId, @CatId, '{}', '{}', 'slug-c', 'pending_approval', 0, @Now, @Now);
            INSERT INTO "ApprovalComments" ("ProductId", "UserId", "Role", "Message", "Type", "CreatedAt")
            VALUES
              ('pa_b', 'u1', 'store_manager', 'x', 'submitted', @Now),
              ('pa_c', 'u2', 'store_manager', 'x', 'submitted', @Now);
            """,
            new { StoreId = storeId, CatId = catId, Now = now });

        var persistence = new SqlCatalogPersistence(_fixture.ConnectionString);

        var (page1, total, next1) = await persistence.ListPendingApprovalsForStoreAsync("t1", storeId, 1, null);
        page1.Should().HaveCount(1);
        total.Should().BeGreaterThanOrEqualTo(2);
        next1.Should().Be(page1[0].Id);

        var (page2, _, next2) = await persistence.ListPendingApprovalsForStoreAsync("t1", storeId, 1, next1);
        page2.Should().HaveCount(1);
        page2[0].Id.Should().NotBe(page1[0].Id);
        next2.Should().BeNull();
    }
}
