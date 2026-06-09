using dCMS.Billing.Domain;
using dCMS.Provisioning.Domain;

namespace dCMS.Web.Billing;

public sealed class TenantEntitlementPublisher(
    ITenantEntitlementRepository repository,
    ITenantEntitlementStore store,
    ITenantFeatureOverrideRepository? featureOverrides = null) : ITenantEntitlementPublisher
{
    public async Task PublishFromRepositoryAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Tenant subscription not found.");

        var version = await store.BumpVersionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var features = await MergeFeaturesAsync(tenantId, record.Features, cancellationToken).ConfigureAwait(false);

        var snapshot = TenantEntitlementSnapshot.Create(
            record.TenantId,
            record.PlanCode,
            record.SubscriptionState,
            record.ManualInvoiceStatus,
            record.TenantActive,
            record.TrialEndsAt,
            record.MaxBrands,
            record.MaxActiveProducts,
            features,
            version);

        await store.PublishAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> MergeFeaturesAsync(
        string tenantId,
        IReadOnlyList<string> planFeatures,
        CancellationToken cancellationToken)
    {
        if (featureOverrides is null)
            return planFeatures;

        var overrides = await featureOverrides.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (overrides.Count == 0)
            return planFeatures;

        var set = new HashSet<string>(planFeatures, StringComparer.OrdinalIgnoreCase);
        foreach (var o in overrides)
        {
            if (o.Enabled)
                set.Add(o.Feature);
            else
                set.Remove(o.Feature);
        }

        return set.ToList();
    }
}
