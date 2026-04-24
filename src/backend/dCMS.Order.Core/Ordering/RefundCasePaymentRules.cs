namespace dCMS.Order.Core.Ordering;

/// <summary>
/// DAI-651 / DAI-655 — latest payment transaction must be in a state that implies money movement for refund-case visibility.
/// </summary>
public static class RefundCasePaymentRules
{
    public static bool IsQualifyingLatestTransactionStatus(string? status)
    {
        var s = (status ?? "").Trim().ToLowerInvariant();
        return s is "succeeded" or "refunded" or "initiated" or "completed";
    }
}
