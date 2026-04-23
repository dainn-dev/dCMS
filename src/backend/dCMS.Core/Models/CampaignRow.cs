namespace dCMS.Core.Models;

/// <summary>
/// Tenant-scoped campaign row (DAI-598).
/// Mechanics and qualifiers stored as JSON strings; caller deserialises to kind-specific types.
/// </summary>
public sealed record CampaignRow(
    string          Id,
    string          TenantId,
    string          Code,
    string          NameJson,
    string          EditorKind,
    string          WorkflowState,
    string          Channel,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    string          ActiveDaysJson,
    string          ActiveMonthsJson,
    string          QualifiersJson,
    string          MechanicsJson,
    string          PromotionDetailsJson,
    string          Budget,
    string          Audience,
    int             Conversions,
    DateTimeOffset  CreatedAt,
    DateTimeOffset  UpdatedAt)
{
    // ── Valid domain values ───────────────────────────────────────────────────

    public static readonly IReadOnlySet<string> ValidEditorKinds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "pwp-item", "pwp-discount", "mix-match", "product-discount", "after-sales" };

    public static readonly IReadOnlySet<string> ValidWorkflowStates =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "draft", "pending_approval", "approved", "active", "deactivated", "archived", "rejected" };

    public static readonly IReadOnlySet<string> ValidChannels =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Email", "SMS", "Push", "Web" };

    // ── Workflow DAG ──────────────────────────────────────────────────────────
    // Allowed transitions: from → [to, ...]
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"]            = new HashSet<string> { "pending_approval", "archived" },
            ["pending_approval"] = new HashSet<string> { "approved", "rejected", "archived" },
            ["approved"]         = new HashSet<string> { "active", "archived" },
            ["active"]           = new HashSet<string> { "deactivated", "archived" },
            ["deactivated"]      = new HashSet<string> { "active", "archived" },
            ["rejected"]         = new HashSet<string> { "draft", "archived" },
            ["archived"]         = new HashSet<string>(StringComparer.OrdinalIgnoreCase),  // terminal
        };

    /// <summary>Returns true if transitioning from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
    public static bool CanTransitionTo(string from, string to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Code must be upper-snake-case: letters, digits, underscores. Max 100.</summary>
    public static bool IsValidCode(string code) =>
        !string.IsNullOrWhiteSpace(code) &&
        code.Length <= 100 &&
        System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Z][A-Z0-9_]*$");
}

/// <summary>Single row from CampaignWorkflowHistory (DAI-598).</summary>
public sealed record CampaignWorkflowHistoryRow(
    int            Id,
    string         CampaignId,
    string         TenantId,
    string         ActorUserId,
    string         FromState,
    string         ToState,
    string         Comment,
    DateTimeOffset CreatedAt);
