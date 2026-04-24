using dCMS.Order.Core.Ordering;

namespace dCMS.Order.Tests.Unit;

public sealed class RefundCaseStatusMapsTests
{
    [Theory]
    [InlineData("success", "Success")]
    [InlineData("SUCCESS", "Success")]
    [InlineData("failed", "Rejected")]
    [InlineData("processing", "Processing")]
    [InlineData("pending_refund", "Pending")]
    [InlineData("pending", "Pending")]
    [InlineData("", "Pending")]
    [InlineData("weird", "Pending")]
    public void UiToDisplay_maps_db_to_ui_label(string? raw, string expected) =>
        Assert.Equal(expected, RefundCaseStatusMaps.UiToDisplay(raw));

    [Theory]
    [InlineData("Success", "success")]
    [InlineData("REJECTED", "failed")]
    [InlineData("Processing", "Processing")]
    [InlineData("Pending Refund", "pending_refund")]
    [InlineData("pending_refund", "pending_refund")]
    [InlineData("pending", "pending_refund")]
    [InlineData("garbage", null)]
    [InlineData("", null)]
    public void UiPatchToDb_maps_canonical_patch_body(string? ui, string? expected) =>
        Assert.Equal(expected, RefundCaseStatusMaps.UiPatchToDb(ui));

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("pending", true)]
    [InlineData("Processing", true)]
    [InlineData("Success", true)]
    [InlineData("Rejected", true)]
    [InlineData("", false)]
    [InlineData("Complete", false)]
    public void IsCanonicalUiStatus(string? ui, bool expected) =>
        Assert.Equal(expected, RefundCaseStatusMaps.IsCanonicalUiStatus(ui));

    [Fact]
    public void UiListFilterToQuery_null_when_unfiltered() =>
        Assert.Null(RefundCaseStatusMaps.UiListFilterToQuery(null));

    [Theory]
    [InlineData("success", "success")]
    [InlineData("rejected", "failed")]
    [InlineData("pending refund", null, true)] // Pending() filter
    public void UiListFilterToQuery_maps_keywords(string ui, string? exactDb, bool pending = false)
    {
        var f = RefundCaseStatusMaps.UiListFilterToQuery(ui);
        Assert.NotNull(f);
        if (pending)
        {
            Assert.True(f!.MatchPending);
            Assert.Null(f.ExactDbValue);
        }
        else
        {
            Assert.False(f!.MatchPending);
            Assert.Equal(exactDb, f.ExactDbValue);
        }
    }

    [Fact]
    public void UiListFilterToQuery_unknown_returns_null() =>
        Assert.Null(RefundCaseStatusMaps.UiListFilterToQuery("not-a-filter"));
}
