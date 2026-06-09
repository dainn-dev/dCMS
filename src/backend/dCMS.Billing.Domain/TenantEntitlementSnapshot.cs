namespace dCMS.Billing.Domain;

/// <summary>Cached tenant billing/entitlement view used by gateway and services.</summary>
public sealed class TenantEntitlementSnapshot
{
    public required string TenantId { get; init; }
    public required PlanCode PlanCode { get; init; }
    public required TenantSubscriptionState SubscriptionState { get; init; }
    public required ManualInvoiceStatus ManualInvoiceStatus { get; init; }
    public required bool TenantActive { get; init; }
    public DateTimeOffset? TrialEndsAt { get; init; }
    public required int MaxBrands { get; init; }
    public required int MaxActiveProducts { get; init; }
    public required IReadOnlySet<string> Features { get; init; }
    public required long Version { get; init; }

    public bool IsOperational => ResolveOperationalReason() is null;

    public string? ResolveOperationalReason()
    {
        if (!TenantActive)
            return EntitlementErrorCodes.TenantInactive;

        return SubscriptionState switch
        {
            TenantSubscriptionState.Suspended => EntitlementErrorCodes.SubscriptionSuspended,
            TenantSubscriptionState.Cancelled => EntitlementErrorCodes.SubscriptionCancelled,
            TenantSubscriptionState.Trial when TrialEndsAt is { } end && end <= DateTimeOffset.UtcNow
                => EntitlementErrorCodes.TrialExpired,
            TenantSubscriptionState.Trial or TenantSubscriptionState.Active => null,
            _ => EntitlementErrorCodes.EntitlementUnavailable,
        };
    }

    public static TenantEntitlementSnapshot Create(
        string tenantId,
        PlanCode planCode,
        TenantSubscriptionState subscriptionState,
        ManualInvoiceStatus manualInvoiceStatus,
        bool tenantActive,
        DateTimeOffset? trialEndsAt,
        int maxBrands,
        int maxActiveProducts,
        IEnumerable<string> features,
        long version) =>
        new()
        {
            TenantId = tenantId,
            PlanCode = planCode,
            SubscriptionState = subscriptionState,
            ManualInvoiceStatus = manualInvoiceStatus,
            TenantActive = tenantActive,
            TrialEndsAt = trialEndsAt,
            MaxBrands = maxBrands,
            MaxActiveProducts = maxActiveProducts,
            Features = features.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Version = version,
        };
}
