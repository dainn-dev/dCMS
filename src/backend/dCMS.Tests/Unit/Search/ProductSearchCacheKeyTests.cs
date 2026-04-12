using dCMS.Core.Search;
using FluentAssertions;

namespace dCMS.Tests.Unit.Search;

public sealed class ProductSearchCacheKeyTests
{
    [Fact]
    public void Same_logical_query_produces_same_hash_regardless_of_attribute_order()
    {
        var a = new ProductSearchQuery("T1", "S1", "Phone", true, 5, 20, null, 100, 500, ProductSearchSort.PriceDesc,
            new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" }, true);
        var b = new ProductSearchQuery("T1", "S1", "Phone", true, 5, 20, null, 100, 500, ProductSearchSort.PriceDesc,
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, true);

        ProductSearchCacheKey.ComputeHash(a).Should().Be(ProductSearchCacheKey.ComputeHash(b));
        ProductSearchCacheKey.RedisKey("S1", ProductSearchCacheKey.ComputeHash(a)).Should().StartWith("dcms:search:S1:");
    }
}
