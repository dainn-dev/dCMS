using dCMS.Core.Services;
using FluentAssertions;
using Axis = dCMS.Core.Services.ProductVariantGeneratorService.VariantAxisDefinition;

namespace dCMS.Tests.Unit.Services;

public sealed class ProductVariantGeneratorServiceTests
{
    [Fact]
    public void GenerateCombinations_cartesian_multiplies_axis_sizes()
    {
        var axes = new[]
        {
            new Axis(2, new[] { 10, 11 }),
            new Axis(7, new[] { 20, 21, 22 })
        };

        var combos = ProductVariantGeneratorService.GenerateCombinations(axes);

        combos.Should().HaveCount(6);
        combos.Should().OnlyContain(c => c.Count == 2 && c.ContainsKey(2) && c.ContainsKey(7));
    }

    [Fact]
    public void GenerateCombinations_returns_empty_when_any_axis_has_no_values()
    {
        var axes = new[] { new Axis(1, new[] { 1 }), new Axis(2, Array.Empty<int>()) };

        var combos = ProductVariantGeneratorService.GenerateCombinations(axes);

        combos.Should().BeEmpty();
    }

    [Fact]
    public void ComputeCombinationHash_sorts_by_attribute_id_in_canonical_string()
    {
        var forward = new Dictionary<int, int> { [2] = 5, [7] = 12 };
        var reverse = new Dictionary<int, int> { [7] = 12, [2] = 5 };

        var h1 = ProductVariantGeneratorService.ComputeCombinationHash(forward);
        var h2 = ProductVariantGeneratorService.ComputeCombinationHash(reverse);

        h1.Should().Be(h2);
        h1.Should().HaveLength(64);
        h1.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void GenerateCombinations_throws_on_duplicate_attribute_axis()
    {
        var axes = new[] { new Axis(1, new[] { 1 }), new Axis(1, new[] { 2 }) };

        var act = () => ProductVariantGeneratorService.GenerateCombinations(axes);

        act.Should().Throw<ArgumentException>().WithParameterName("axes");
    }
}
