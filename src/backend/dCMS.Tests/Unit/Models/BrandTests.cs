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
    // DAI-743 relaxed cases — bulk-import legacy codes:
    [InlineData("acqua-di-parma")]
    [InlineData("10-DEEP")]
    [InlineData("a")]
    [InlineData("CASMI7721")]                  // no dash
    [InlineData("ABCDEFGHIJ-1234567890")]      // long but ≤ 64
    public void Create_succeeds_for_valid_codes(string code)
    {
        var brand = Brand.Create("t1", code, "Test Brand", "", "", true, Now);
        brand.Code.Should().Be(code);          // DAI-743: casing preserved
        brand.TenantId.Should().Be("t1");
        brand.Active.Should().BeTrue();
        brand.CreatedAt.Should().Be(Now);
        brand.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("")]                                              // empty
    [InlineData("-leading-dash")]                                 // first char must be alnum
    [InlineData("name with spaces")]                              // disallowed char
    [InlineData("name_with_underscore")]                          // underscore disallowed
    [InlineData("contains/slash")]                                // disallowed char
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ12345678901234567890ABCDEFGHIJKLMNOPQRSTU")] // > 64 chars
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
    public void Create_preserves_code_casing_after_DAI743()
    {
        var brand = Brand.Create("t1", "Acqua-di-Parma", "Brand", "", "", true, Now);
        brand.Code.Should().Be("Acqua-di-Parma");
    }

    [Fact]
    public void Create_trims_code_whitespace()
    {
        var brand = Brand.Create("t1", "  CAS-7721  ", "Brand", "", "", true, Now);
        brand.Code.Should().Be("CAS-7721");
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
    [InlineData("AB-1", true)]
    [InlineData("ABCDE-999999", true)]
    // DAI-743 relaxed:
    [InlineData("cas-7721", true)]
    [InlineData("CAS7721", true)]
    [InlineData("acqua-di-parma", true)]
    [InlineData("10-DEEP", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData("-x", false)]
    [InlineData("has space", false)]
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
