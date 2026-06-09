using System.Net;
using System.Net.Http.Headers;
using dCMS.AspNetCore.Auth;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Access;

[Collection("CatalogApiAuth")]
public sealed class PublicCatalogAccessTests(CatalogApiAuthFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    [SkippableFact]
    public async Task Public_product_search_anonymous_returns_200()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        var url = $"/api/v1/products?tenantId={SaasCoreSeeds.TenantA}&storeId={SaasCoreSeeds.StoreA1}&q=test";
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Catalog_write_without_token_returns_401()
    {
        Skip();
        using var client = fixture.Factory!.CreateClient();
        var url = $"/api/v1/tenants/{SaasCoreSeeds.TenantA}/stores/{SaasCoreSeeds.StoreA1}/products";
        var response = await client.PostAsync(url, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
