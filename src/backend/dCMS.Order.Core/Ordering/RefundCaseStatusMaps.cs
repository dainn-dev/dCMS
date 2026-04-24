namespace dCMS.Order.Core.Ordering;

/// <summary>Maps refund-case status between API/UI and persisted <c>Orders.RefundStatus</c> (DAI-651 / DAI-653).</summary>
public static class RefundCaseStatusMaps
{
    public static string UiToDisplay(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "success" => "Success",
            "failed" => "Rejected",
            "processing" => "Processing",
            "pending_refund" or "pending" => "Pending",
            "" => "Pending",
            _ => "Pending",
        };

    /// <summary>UI / PATCH body → DB <c>RefundStatus</c> value.</summary>
    public static string? UiPatchToDb(string? ui) =>
        (ui ?? "").Trim().ToLowerInvariant() switch
        {
            "success" => "success",
            "failed" => "failed",
            "rejected" => "failed",
            "processing" => "Processing",
            "pending refund" => "pending_refund",
            "pending_refund" => "pending_refund",
            "pending" => "pending_refund",
            _ => null,
        };

    /// <summary>DAI-654: canonical UI status labels (case-insensitive) accepted on PUT/PATCH.</summary>
    public static bool IsCanonicalUiStatus(string? ui)
    {
        if (string.IsNullOrWhiteSpace(ui)) return false;
        var s = ui.Trim();
        return s.Equals("Pending", StringComparison.OrdinalIgnoreCase)
               || s.Equals("Processing", StringComparison.OrdinalIgnoreCase)
               || s.Equals("Success", StringComparison.OrdinalIgnoreCase)
               || s.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Optional list filter: UI label / keyword → DB filter bucket (null = no filter).</summary>
    public static RefundStatusListFilter? UiListFilterToQuery(string? statusUi)
    {
        if (string.IsNullOrWhiteSpace(statusUi))
            return null;
        var s = statusUi.Trim().ToLowerInvariant();
        return s switch
        {
            "success" => RefundStatusListFilter.ExactDb("success"),
            "failed" or "rejected" => RefundStatusListFilter.ExactDb("failed"),
            "processing" => RefundStatusListFilter.ExactDb("Processing"),
            "pending refund" or "pending_refund" or "pending" => RefundStatusListFilter.Pending(),
            _ => null,
        };
    }
}

/// <summary>How to filter <c>Orders.RefundStatus</c> when listing refund cases.</summary>
public sealed record RefundStatusListFilter
{
    public bool MatchPending { get; init; }
    public string? ExactDbValue { get; init; }

    public static RefundStatusListFilter Pending() => new() { MatchPending = true };

    public static RefundStatusListFilter ExactDb(string db) => new() { ExactDbValue = db };
}
