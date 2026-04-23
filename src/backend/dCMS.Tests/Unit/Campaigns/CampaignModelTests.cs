using dCMS.Core.Models;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Campaigns;

/// <summary>DAI-602: CampaignRow workflow DAG unit tests.</summary>
public sealed class CampaignModelTests
{
    // ── Code validation ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("SPRING26",    true)]
    [InlineData("CART_ABN",    true)]
    [InlineData("A",           true)]
    [InlineData("A1_B2",       true)]
    [InlineData("lowercase",   false)]
    [InlineData("123START",    false)]
    [InlineData("WITH-DASH",   false)]
    [InlineData("WITH SPACE",  false)]
    [InlineData("",            false)]
    public void IsValidCode_returns_expected(string code, bool expected) =>
        CampaignRow.IsValidCode(code).Should().Be(expected);

    // ── Workflow DAG ──────────────────────────────────────────────────────────

    [Theory]
    // Valid transitions
    [InlineData("draft",            "pending_approval", true)]
    [InlineData("draft",            "archived",         true)]
    [InlineData("pending_approval", "approved",         true)]
    [InlineData("pending_approval", "rejected",         true)]
    [InlineData("pending_approval", "archived",         true)]
    [InlineData("approved",         "active",           true)]
    [InlineData("approved",         "archived",         true)]
    [InlineData("active",           "deactivated",      true)]
    [InlineData("active",           "archived",         true)]
    [InlineData("deactivated",      "active",           true)]
    [InlineData("deactivated",      "archived",         true)]
    [InlineData("rejected",         "draft",            true)]
    [InlineData("rejected",         "archived",         true)]
    // Invalid transitions
    [InlineData("draft",            "active",           false)]
    [InlineData("draft",            "approved",         false)]
    [InlineData("draft",            "rejected",         false)]
    [InlineData("pending_approval", "active",           false)]
    [InlineData("approved",         "draft",            false)]
    [InlineData("active",           "draft",            false)]
    [InlineData("archived",         "draft",            false)]
    [InlineData("archived",         "active",           false)]
    public void CanTransitionTo_returns_expected(string from, string to, bool expected) =>
        CampaignRow.CanTransitionTo(from, to).Should().Be(expected);

    // ── Valid editor kinds ────────────────────────────────────────────────────

    [Theory]
    [InlineData("pwp-item",          true)]
    [InlineData("pwp-discount",      true)]
    [InlineData("mix-match",         true)]
    [InlineData("product-discount",  true)]
    [InlineData("after-sales",       true)]
    [InlineData("unknown-kind",      false)]
    [InlineData("",                  false)]
    public void ValidEditorKinds_contains_expected(string kind, bool expected) =>
        CampaignRow.ValidEditorKinds.Contains(kind).Should().Be(expected);
}
