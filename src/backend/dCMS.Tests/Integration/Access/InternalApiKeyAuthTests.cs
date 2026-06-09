using System.Net;
using dCMS.Catalog.Api.Internal;
using FluentAssertions;
using dCMS.Promotions.Api.Internal;
using dCMS.Tests.Integration.Promotions;
using Xunit;

namespace dCMS.Tests.Integration.Access;

[Collection("CatalogApiAuth")]
public sealed class InternalApiKeyAuthTests(CatalogApiAuthFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    [SkippableFact]
    public async Task Internal_catalog_missing_api_key_returns_401()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(
            $"/internal/catalog/tenants/{SaasCoreSeeds.TenantA}/products/p1/exists");
        await SaasCoreHttpAssert.AssertAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [SkippableFact]
    public async Task Internal_catalog_wrong_api_key_returns_403()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Add(InternalCatalogApiKeyEndpointFilter.HeaderName, "wrong-key");
        var response = await client.GetAsync(
            $"/internal/catalog/tenants/{SaasCoreSeeds.TenantA}/products/p1/exists");
        await SaasCoreHttpAssert.AssertAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [SkippableFact]
    public async Task Internal_catalog_valid_api_key_returns_200()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Add(InternalCatalogApiKeyEndpointFilter.HeaderName, CatalogApiAuthFixture.InternalApiKey);
        var response = await client.GetAsync(
            $"/internal/catalog/tenants/{SaasCoreSeeds.TenantA}/products/p1/exists");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection("PromotionsApiAuth")]
public sealed class InternalPromotionsApiKeyAuthTests(PromotionsApiAuthFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    [SkippableFact]
    public async Task Internal_promotions_missing_api_key_returns_401()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(
            $"/internal/promotions/tenants/{SaasCoreSeeds.TenantA}/campaigns/c1/workflow-state");
        await SaasCoreHttpAssert.AssertAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [SkippableFact]
    public async Task Internal_promotions_valid_api_key_returns_404_not_401()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Add(InternalPromotionsApiKeyEndpointFilter.HeaderName, PromotionsApiAuthFixture.InternalApiKey);
        var response = await client.GetAsync(
            $"/internal/promotions/tenants/{SaasCoreSeeds.TenantA}/campaigns/c1/workflow-state");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
