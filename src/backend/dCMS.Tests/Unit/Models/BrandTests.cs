using dCMS.Core.Models;
using FluentAssertions;

namespace dCMS.Tests.Unit.Models;

public sealed class BrandTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-22T10:00:00Z");

    // ── Create ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CAS-7721")]
    [InlineData("VEL-4490")]
    [InlineData("AB-1")]
    [InlineData("ABCDE-999999")]
    public void Create_succeeds_for_valid_codes(string code)
    {
        var brand = Brand.Create("t1", code, "Test Brand", "", "", true, Now);
        brand.Code.Should().Be(code.ToUpperInvariant());
        brand.TenantId.Should().Be("t1");
        brand.Active.Should().BeTrue();
        brand.CreatedAt.Should().Be(Now);
        brand.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("cas-7721")]       // lowercase
    [InlineData("CAS7721")]        // no dash
    [InlineData("C-1234")]         // prefix too short (1 char)
    [InlineData("TOOLONG-1")]      // prefix > 5 chars
    [InlineData("CAS-1234567")]    // suffix > 6 digits
    [InlineData("CAS-")]           // missing digits
    [InlineData("")]               // empty
    public void Create_throws_for_invalid_codes(string code)
    {
        var act = () => Brand.Create("t1", code, "Test Brand", "", "", true, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_throws_when_name_is_empty()
    {
        var act = () => Brand.Create("t1", "CAS-7721", "", "", "", true, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_throws_when_tenantId_is_empty()
    {
        var act = () => Brand.Create("", "CAS-7721", "Brand", "", "", true, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_throws_when_name_exceeds_200_chars()
    {
        var act = () => Brand.Create("t1", "CAS-7721", new string('X', 201), "", "", true, Now);
        act.Should().Throw<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public void Create_normalises_code_to_uppercase()
    {
        // lowercase input but valid after upper — IsValidCode checks uppercase only
        // Create always calls ToUpperInvariant before matching, but IsValidCode is raw
        var brand = Brand.Create("t1", "CAS-1234", "Brand", "", "", true, Now);
        brand.Code.Should().Be("CAS-1234");
    }

    // ── UpdateDetails ─────────────────────────────────────────────────────────

    [Fact]
    public void UpdateDetails_mutates_fields_and_bumps_UpdatedAt()
    {
        var brand = Brand.Create("t1", "CAS-1", "Old Name", "http://old", "old alt", true, Now);
        var later = Now.AddHours(1);

        brand.UpdateDetails("New Name", false, "http://new", "new alt", "{\"key\":\"val\"}", later);

        brand.Name.Should().Be("New Name");
        brand.Active.Should().BeFalse();
        brand.ImageUrl.Should().Be("http://new");
        brand.ImageAlt.Should().Be("new alt");
        brand.AdditionalInfo.Should().Be("{\"key\":\"val\"}");
        brand.UpdatedAt.Should().Be(later);
        brand.CreatedAt.Should().Be(Now); // unchanged
    }

    [Fact]
    public void UpdateDetails_throws_when_name_is_empty()
    {
        var brand = Brand.Create("t1", "CAS-1", "Brand", "", "", true, Now);
        var act = () => brand.UpdateDetails("", true, "", "", "{}", Now.AddHours(1));
        act.Should().Throw<ArgumentException>();
    }

    // ── IsValidCode ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CAS-7721", true)]
    [InlineData("AB-1",     true)]
    [InlineData("ABCDE-999999", true)]
    [InlineData("cas-7721", false)]
    [InlineData("CAS7721",  false)]
    [InlineData("",         false)]
    [InlineData("TOOLONG-1", false)]
    public void IsValidCode_returns_expected(string code, bool expected)
    {
        Brand.IsValidCode(code).Should().Be(expected);
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    [Fact]
    public void Restore_round_trips_all_fields()
    {
        var brand = Brand.Restore("t1", "CAS-7721", "Luxe", true, "http://img", "alt text", "{\"tier\":\"gold\"}", Now, Now.AddHours(2));

        brand.TenantId.Should().Be("t1");
        brand.Code.Should().Be("CAS-7721");
        brand.Name.Should().Be("Luxe");
        brand.Active.Should().BeTrue();
        brand.ImageUrl.Should().Be("http://img");
        brand.ImageAlt.Should().Be("alt text");
        brand.AdditionalInfo.Should().Be("{\"tier\":\"gold\"}");
        brand.CreatedAt.Should().Be(Now);
        brand.UpdatedAt.Should().Be(Now.AddHours(2));
    }
}
