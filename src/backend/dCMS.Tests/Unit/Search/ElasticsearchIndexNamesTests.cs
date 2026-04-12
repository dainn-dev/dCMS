using dCMS.Infrastructure.Search;
using FluentAssertions;

namespace dCMS.Tests.Unit.Search;

public sealed class ElasticsearchIndexNamesTests
{
    [Theory]
    [InlineData("Lotte", "dcms-lotte-products")]
    [InlineData("  Acme_Co  ", "dcms-acme_co-products")]
    public void Products_normalizes_tenant_slug(string tenant, string expected) =>
        ElasticsearchIndexNames.Products(tenant).Should().Be(expected);

    [Theory]
    [InlineData("Lotte", 1, "dcms-lotte-products-v1")]
    [InlineData("Acme_Co", 2, "dcms-acme_co-products-v2")]
    public void ProductsBackingIndex_appends_version_suffix(string tenant, int version, string expected) =>
        ElasticsearchIndexNames.ProductsBackingIndex(tenant, version).Should().Be(expected);
}
