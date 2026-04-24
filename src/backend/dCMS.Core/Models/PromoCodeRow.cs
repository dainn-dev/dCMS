namespace dCMS.Core.Models;

/// <summary>
/// Tenant-scoped promo code row (DAI-659). DAI-664: optional label, min spend, validity window.
/// <see cref="SubmittedByUserId"/> and <see cref="SubmittedAt"/> are populated on list queries only (last transition to pending_approval).
/// </summary>
public sealed record PromoCodeRow(
    string Id,
    string TenantId,
    string Code,
    string NameJson,
    string DiscountType,
    string DiscountValue,
    string WorkflowState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string PromoTypeLabel = "",
    string MinSpend = "",
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    string? SubmittedByUserId = null,
    DateTimeOffset? SubmittedAt = null)
{
    public static readonly IReadOnlySet<string> ValidDiscountTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "percentage",
            "fixed",
            "free_shipping",
        };

    public static readonly IReadOnlySet<string> ValidWorkflowStates =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "draft",
            "pending_approval",
            "approved",
            "rejected",
            "archived",
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = new HashSet<string> { "pending_approval", "archived" },
            ["pending_approval"] = new HashSet<string> { "approved", "rejected", "archived" },
            ["approved"] = new HashSet<string> { "archived" },
            ["rejected"] = new HashSet<string> { "draft", "archived" },
            ["archived"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };

    public static bool CanTransitionTo(string from, string to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Upper-snake promo code, same rules as <see cref="CampaignRow.IsValidCode"/>.</summary>
    public static bool IsValidCode(string code) => CampaignRow.IsValidCode(code);
}
