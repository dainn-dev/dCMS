using System.Net;
using System.Net.Http.Headers;
using dCMS.AspNetCore.Auth;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Access;

[Collection("CatalogApiAuth")]
public sealed class CatalogImportAuthTests(CatalogApiAuthFixture fixture)
{
    private static string ImportsUrl(string tenantId) => $"/api/v1/tenants/{tenantId}/imports";

    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    [SkippableFact]
    public async Task List_imports_without_bearer_returns_401()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(ImportsUrl(SaasCoreSeeds.TenantA));
        await SaasCoreHttpAssert.AssertAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [SkippableFact]
    public async Task List_imports_store_staff_returns_403()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SaasCoreJwtFactory.MintForRole(DcmsRoles.StoreStaff, SaasCoreSeeds.TenantA, SaasCoreSeeds.StoreA1));

        var response = await client.GetAsync(ImportsUrl(SaasCoreSeeds.TenantA));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task List_imports_store_manager_home_tenant_returns_200()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SaasCoreJwtFactory.MintForRole(DcmsRoles.StoreManager, SaasCoreSeeds.TenantA, SaasCoreSeeds.StoreA1));

        var response = await client.GetAsync(ImportsUrl(SaasCoreSeeds.TenantA));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task List_imports_cross_tenant_returns_403()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SaasCoreJwtFactory.MintForRole(DcmsRoles.StoreManager, SaasCoreSeeds.TenantA, SaasCoreSeeds.StoreA1));

        var response = await client.GetAsync(ImportsUrl(SaasCoreSeeds.TenantB));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
