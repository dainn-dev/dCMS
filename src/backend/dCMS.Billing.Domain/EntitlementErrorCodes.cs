namespace dCMS.Billing.Domain;

public static class EntitlementErrorCodes
{
    public const string TenantInactive = "tenant_inactive";
    public const string SubscriptionSuspended = "subscription_suspended";
    public const string SubscriptionCancelled = "subscription_cancelled";
    public const string TrialExpired = "trial_expired";
    public const string QuotaExceeded = "quota_exceeded";
    public const string EntitlementDenied = "entitlement_denied";
    public const string EntitlementUnavailable = "entitlement_unavailable";
}
