using dCMS.Core.Models;
using dCMS.Core.Search;
using FluentAssertions;

namespace dCMS.Tests.Unit.Search;

public sealed class ProductDocumentBuilderTests
{
    [Fact]
    public void Build_maps_stock_and_has_in_stock_flag()
    {
        var now = DateTimeOffset.UtcNow;
        var product = Product.Restore("prod_1", "t1", "s1", 10, """{"vi":"Áo"}""", "{}", "slug-a", ProductStatus.Active, 3,
            now, now);
        var v1 = ProductVariant.Restore("var_1", "prod_1", "SKU-1", "hash1", "active", 0, "", 1_000_000);
        var v2 = ProductVariant.Restore("var_2", "prod_1", "SKU-2", "hash2", "active", 1, "", 2_000_000);
        var stock = new Dictionary<string, VariantStockSummary>(StringComparer.Ordinal)
        {
            ["var_1"] = new VariantStockSummary(0, false),
            ["var_2"] = new VariantStockSummary(5, true)
        };
        var payload = new ProductIndexPayload(
            product,
            new[] { v1, v2 },
            new[] { 1, 5, 10 },
            "/1/5/10/",
            stock,
            new Dictionary<string, string> { ["material"] = "cotton" },
            2,
            "VND",
            "brand_x");

        var doc = ProductDocumentBuilder.Build(payload);

        doc.Id.Should().Be("prod_1");
        doc.CategoryAncestors.Should().Equal(1, 5, 10);
        doc.CategoryPath.Should().Be("/1/5/10/");
        doc.BrandId.Should().Be("brand_x");
        doc.StoreCurrency.Should().Be("VND");
        doc.SnapshotVersion.Should().Be(2);
        doc.Attributes["material"].Should().Be("cotton");
        doc.HasInStockVariant.Should().BeTrue();
        doc.MinBasePrice.Amount.Should().Be(1_000_000);
        doc.Variants.Single(x => x.VariantId == "var_1").BasePrice.Amount.Should().Be(1_000_000);
        doc.Variants.Single(x => x.VariantId == "var_2").BasePrice.Amount.Should().Be(2_000_000);
        doc.Variants.Should().HaveCount(2);
        doc.Variants.Single(x => x.VariantId == "var_2").AvailableQty.Should().Be(5);
        doc.Variants.Single(x => x.VariantId == "var_2").InStock.Should().BeTrue();
        doc.Name.Should().ContainKey("vi");
    }

    [Fact]
    public void ParseObjectStringValues_coerces_non_string_json_to_text()
    {
        var d = ProductDocumentBuilder.ParseObjectStringValues("""{"vi":"A","n":1,"b":true}""");
        d["vi"].Should().Be("A");
        d["n"].Should().Be("1");
        d["b"].Should().Be("true");
    }

    [Fact]
    public void ParseObjectStringValues_returns_empty_on_invalid_json()
    {
        ProductDocumentBuilder.ParseObjectStringValues("{not json").Should().BeEmpty();
    }
}
