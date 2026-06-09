namespace dCMS.Billing.Domain;

public interface IEntitlementGuard
{
    Task<TenantEntitlementSnapshot> EnsureOperationalAsync(string tenantId, CancellationToken cancellationToken = default);

    Task EnsureFeatureAsync(string tenantId, string feature, CancellationToken cancellationToken = default);

    Task EnsureQuotaAsync(
        string tenantId,
        string quotaName,
        int requestedUsage,
        CancellationToken cancellationToken = default);
}
