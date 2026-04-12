using dCMS.Core.ValueObjects;
using FluentAssertions;

namespace dCMS.Tests.Unit.ValueObjects;

public sealed class MultilangJsonTests
{
    [Fact]
    public void ValidateNameRequiredVi_accepts_vi_with_optional_en()
    {
        var act = () => MultilangJson.ValidateNameRequiredVi("""{"vi":"Tiếng Việt","en":"English"}""");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"en":"only"}""")]
    public void ValidateNameRequiredVi_rejects_invalid(string json)
    {
        var act = () => MultilangJson.ValidateNameRequiredVi(json);

        act.Should().Throw<ArgumentException>();
    }
}
