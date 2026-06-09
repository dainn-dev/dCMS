namespace dCMS.Billing.Domain;

public static class EntitlementQuotaNames
{
    public const string MaxActiveProducts = "max_active_products";
    public const string MaxBrands = "max_brands";
}

public sealed class EntitlementGuard(ITenantEntitlementStore store) : IEntitlementGuard
{
    private readonly ITenantEntitlementStore _store = store;

    public async Task<TenantEntitlementSnapshot> EnsureOperationalAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await ResolveSnapshotAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var reason = snapshot.ResolveOperationalReason();
        if (reason is not null)
            throw new TenantEntitlementException(reason, OperationalMessage(reason));

        return snapshot;
    }

    public async Task EnsureFeatureAsync(
        string tenantId,
        string feature,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await EnsureOperationalAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!snapshot.Features.Contains(feature))
        {
            throw new TenantEntitlementException(
                EntitlementErrorCodes.EntitlementDenied,
                $"Feature '{feature}' is not included in the current plan.");
        }
    }

    public async Task EnsureQuotaAsync(
        string tenantId,
        string quotaName,
        int requestedUsage,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await EnsureOperationalAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var limit = quotaName switch
        {
            EntitlementQuotaNames.MaxActiveProducts => snapshot.MaxActiveProducts,
            EntitlementQuotaNames.MaxBrands => snapshot.MaxBrands,
            _ => throw new ArgumentException($"Unknown quota '{quotaName}'.", nameof(quotaName)),
        };

        if (requestedUsage > limit)
        {
            throw new TenantEntitlementException(
                EntitlementErrorCodes.QuotaExceeded,
                $"Quota '{quotaName}' exceeded ({requestedUsage}/{limit}).");
        }
    }

    private async Task<TenantEntitlementSnapshot> ResolveSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var snapshot = await _store.TryGetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            throw new TenantEntitlementException(
                EntitlementErrorCodes.EntitlementUnavailable,
                "Tenant entitlement snapshot is unavailable.");
        }

        return snapshot;
    }

    private static string OperationalMessage(string code) =>
        code switch
        {
            EntitlementErrorCodes.TenantInactive => "Tenant is inactive.",
            EntitlementErrorCodes.SubscriptionSuspended => "Tenant subscription is suspended.",
            EntitlementErrorCodes.SubscriptionCancelled => "Tenant subscription is cancelled.",
            EntitlementErrorCodes.TrialExpired => "Tenant trial has expired.",
            _ => "Tenant is not operational.",
        };
}
