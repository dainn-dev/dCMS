namespace dCMS.Billing.Domain;

public interface ITenantEntitlementPublisher
{
    Task PublishFromRepositoryAsync(string tenantId, CancellationToken cancellationToken = default);
}
