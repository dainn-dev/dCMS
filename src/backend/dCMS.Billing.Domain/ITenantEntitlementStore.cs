namespace dCMS.Billing.Domain;

public interface ITenantEntitlementStore
{
    Task<TenantEntitlementSnapshot?> TryGetAsync(string tenantId, CancellationToken cancellationToken = default);

    Task PublishAsync(TenantEntitlementSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<long> BumpVersionAsync(string tenantId, CancellationToken cancellationToken = default);
}
