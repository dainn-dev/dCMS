using dCMS.Core.Search;
using FluentAssertions;

namespace dCMS.Tests.Unit.Search;

public sealed class ProductCustomFieldsMapperTests
{
    private const string ConfigJson = """
        [
          {"id":"pfld-1","enabled":true,"required":false,"property":"warranty_period","columnLabel":"Warranty","fieldName":"Warranty Period","controlType":"Text Box","targetPage":"General","options":[]},
          {"id":"pfld-2","enabled":true,"required":false,"property":"product_origin","columnLabel":"Origin","fieldName":"Origin","controlType":"Dropdown List","targetPage":"Product Page","options":[{"name":"USA","value":"US"}]},
          {"id":"pfld-3","enabled":false,"required":false,"property":"internal_note","columnLabel":"Note","fieldName":"Note","controlType":"Text Box","targetPage":"General","options":[]},
          {"id":"pfld-4","enabled":true,"required":false,"property":"tags","columnLabel":"Tags","fieldName":"Tags","controlType":"Multiple Select","targetPage":"Product Page","options":[{"name":"New","value":"new"}]}
        ]
        """;

    [Fact]
    public void ToIndexAttributes_maps_enabled_properties_only()
    {
        var defs = ProductCustomFieldsMapper.ParseDefinitions(ConfigJson);
        var values = """{"pfld-1":"12 months","pfld-2":"US","pfld-3":"hidden","pfld-4":"new,featured"}""";

        var attrs = ProductCustomFieldsMapper.ToIndexAttributes(defs, values);

        attrs.Should().ContainKey("warranty_period").WhoseValue.Should().Be("12 months");
        attrs.Should().ContainKey("product_origin").WhoseValue.Should().Be("US");
        attrs.Should().ContainKey("tags").WhoseValue.Should().Be("new,featured");
        attrs.Should().NotContainKey("internal_note");
    }

    [Fact]
    public void ToPublicFields_filters_storefront_targets()
    {
        var defs = ProductCustomFieldsMapper.ParseDefinitions(ConfigJson);
        var values = """{"pfld-1":"12 months","pfld-4":"new,featured"}""";

        var fields = ProductCustomFieldsMapper.ToPublicFields(defs, values, ProductCustomFieldsMapper.IsStorefrontDetailTarget);

        fields.Should().HaveCount(3);
        fields.Should().Contain(f => f.Property == "warranty_period" && (string?)f.Value == "12 months");
        fields.Single(f => f.Property == "tags").Value.Should().BeEquivalentTo(new[] { "new", "featured" });
    }

    [Fact]
    public void FilterableFacetProperties_includes_dropdown_and_multiselect_only()
    {
        var defs = ProductCustomFieldsMapper.ParseDefinitions(ConfigJson);
        var props = ProductCustomFieldsMapper.FilterableFacetProperties(defs);

        props.Should().BeEquivalentTo(new[] { "product_origin", "tags" });
    }
}
