using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

[Collection("CatalogApi")]
public sealed class CatalogApiIntegrationTests(CatalogApiFixture fixture)
{
    [SkippableFact]
    public async Task List_categories_returns_flat_rows_with_is_leaf()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/tenants/t1/stores/s1/categories", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);

        var electronics = items.Single(e => e.GetProperty("slug").GetString() == "electronics");
        electronics.GetProperty("parentId").ValueKind.Should().Be(JsonValueKind.Null);
        electronics.GetProperty("isLeaf").GetBoolean().Should().BeFalse();

        var phones = items.Single(e => e.GetProperty("slug").GetString() == "phones");
        phones.GetProperty("parentId").GetInt32().Should().Be(electronics.GetProperty("id").GetInt32());
        phones.GetProperty("isLeaf").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task List_variant_axes_returns_attributes_and_values()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/tenants/t1/stores/s1/variant-axes", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);

        var color = items.Single(e => e.GetProperty("name").GetString() == "Color");
        color.GetProperty("attributeId").GetInt32().Should().BeGreaterThan(0);
        color.GetProperty("values").GetArrayLength().Should().Be(2);

        var size = items.Single(e => e.GetProperty("name").GetString() == "Size");
        size.GetProperty("values").GetArrayLength().Should().Be(2);
    }
}
