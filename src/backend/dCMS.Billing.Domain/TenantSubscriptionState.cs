namespace dCMS.Billing.Domain;

public enum TenantSubscriptionState
{
    Trial = 0,
    Active = 1,
    Suspended = 2,
    Cancelled = 3,
}

public static class TenantSubscriptionStateExtensions
{
    public static string ToPersistedValue(this TenantSubscriptionState state) =>
        state switch
        {
            TenantSubscriptionState.Trial => "trial",
            TenantSubscriptionState.Active => "active",
            TenantSubscriptionState.Suspended => "suspended",
            TenantSubscriptionState.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

    public static TenantSubscriptionState ParsePersisted(string value) =>
        value switch
        {
            "trial" => TenantSubscriptionState.Trial,
            "active" => TenantSubscriptionState.Active,
            "suspended" => TenantSubscriptionState.Suspended,
            "cancelled" => TenantSubscriptionState.Cancelled,
            _ => throw new ArgumentException($"Unknown subscription state '{value}'.", nameof(value)),
        };
}
