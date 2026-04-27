using System.Text.Json;
using Dapper;
using dCMS.Approval.Api.Routes.Subjects;
using dCMS.Core.Approvals;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace dCMS.Tests.Integration.Approval;

[Collection("catalog-variant-pg")]
public sealed class ProductApprovalSubjectIntegrationTests(dCMS.Tests.Integration.Catalog.CatalogVariantPgFixture fixture)
{
    private readonly dCMS.Tests.Integration.Catalog.CatalogVariantPgFixture _fixture = fixture;

    private static JsonDocument Empty() => JsonDocument.Parse("{}");

    [Fact]
    public async Task EntityType_is_Product()
    {
        var subject = new ProductApprovalSubject(_fixture.ConnectionString);
        subject.EntityType.Should().Be("Product");
    }

    [Fact]
    public async Task ValidateAsync_returns_error_for_missing_product()
    {
        var subject = new ProductApprovalSubject(_fixture.ConnectionString);
        using var doc = Empty();
        var err = await subject.ValidateAsync("t1", "missing-id", ApprovalAction.Approve, doc, default);
        err.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateAsync_returns_null_for_existing_product()
    {
        var subject = new ProductApprovalSubject(_fixture.ConnectionString);
        using var doc = Empty();
        // Fixture seeds product "p1" tenant "t1".
        var err = await subject.ValidateAsync("t1", "p1", ApprovalAction.Approve, doc, default);
        err.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_Approve_sets_IsActive_true()
    {
        var subject = new ProductApprovalSubject(_fixture.ConnectionString);
        using var doc = Empty();

        // Reset to false first to make this test independent of others.
        await using (var c = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await c.ExecuteAsync(
                """UPDATE "Products" SET "IsActive" = FALSE WHERE "TenantId"='t1' AND "Id"='p1';""");
        }

        await subject.ApplyAsync("t1", "p1", ApprovalAction.Approve, doc, "admin", default);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var isActive = await conn.ExecuteScalarAsync<bool>(
            """SELECT "IsActive" FROM "Products" WHERE "TenantId"='t1' AND "Id"='p1';""");
        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_Reject_sets_IsActive_false()
    {
        var subject = new ProductApprovalSubject(_fixture.ConnectionString);
        using var doc = Empty();

        await using (var c = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await c.ExecuteAsync(
                """UPDATE "Products" SET "IsActive" = TRUE WHERE "TenantId"='t1' AND "Id"='p1';""");
        }

        await subject.ApplyAsync("t1", "p1", ApprovalAction.Reject, doc, "admin", default);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var isActive = await conn.ExecuteScalarAsync<bool>(
            """SELECT "IsActive" FROM "Products" WHERE "TenantId"='t1' AND "Id"='p1';""");
        isActive.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_Submit_is_noop_on_activation_flag()
    {
        var subject = new ProductApprovalSubject(_fixture.ConnectionString);
        using var doc = Empty();

        await using (var c = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await c.ExecuteAsync(
                """UPDATE "Products" SET "IsActive" = TRUE WHERE "TenantId"='t1' AND "Id"='p1';""");
        }

        await subject.ApplyAsync("t1", "p1", ApprovalAction.Submit, doc, "admin", default);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var isActive = await conn.ExecuteScalarAsync<bool>(
            """SELECT "IsActive" FROM "Products" WHERE "TenantId"='t1' AND "Id"='p1';""");
        isActive.Should().BeTrue(); // unchanged — Submit is not a finalization signal
    }
}
