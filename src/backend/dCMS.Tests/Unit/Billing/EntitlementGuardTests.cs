using dCMS.Billing.Domain;
using FluentAssertions;

namespace dCMS.Tests.Unit.Billing;

public sealed class EntitlementGuardTests
{
    [Fact]
    public async Task EnsureQuotaAsync_allows_when_under_limit()
    {
        var store = new FakeTenantEntitlementStore(OperationalSnapshot(maxActiveProducts: 5));
        var guard = new EntitlementGuard(store);

        var act = () => guard.EnsureQuotaAsync("t1", EntitlementQuotaNames.MaxActiveProducts, 5);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureQuotaAsync_throws_quota_exceeded_when_over_limit()
    {
        var store = new FakeTenantEntitlementStore(OperationalSnapshot(maxActiveProducts: 5));
        var guard = new EntitlementGuard(store);

        var act = () => guard.EnsureQuotaAsync("t1", EntitlementQuotaNames.MaxActiveProducts, 6);
        var ex = await act.Should().ThrowAsync<TenantEntitlementException>();
        ex.Which.Code.Should().Be(EntitlementErrorCodes.QuotaExceeded);
    }

    [Fact]
    public async Task EnsureFeatureAsync_throws_entitlement_denied_when_feature_missing()
    {
        var store = new FakeTenantEntitlementStore(OperationalSnapshot(features: ["catalog.read"]));
        var guard = new EntitlementGuard(store);

        var act = () => guard.EnsureFeatureAsync("t1", "catalog.write");
        var ex = await act.Should().ThrowAsync<TenantEntitlementException>();
        ex.Which.Code.Should().Be(EntitlementErrorCodes.EntitlementDenied);
    }

    [Fact]
    public async Task EnsureOperationalAsync_throws_when_suspended()
    {
        var snapshot = TenantEntitlementSnapshot.Create(
            "t1", PlanCode.Starter, TenantSubscriptionState.Suspended, ManualInvoiceStatus.None,
            tenantActive: true, trialEndsAt: null, 2, 500, ["catalog.write"], 1);
        var guard = new EntitlementGuard(new FakeTenantEntitlementStore(snapshot));

        var act = () => guard.EnsureOperationalAsync("t1");
        var ex = await act.Should().ThrowAsync<TenantEntitlementException>();
        ex.Which.Code.Should().Be(EntitlementErrorCodes.SubscriptionSuspended);
    }

    [Fact]
    public async Task EnsureOperationalAsync_throws_entitlement_unavailable_on_cache_miss()
    {
        var guard = new EntitlementGuard(new FakeTenantEntitlementStore(null));

        var act = () => guard.EnsureOperationalAsync("t1");
        var ex = await act.Should().ThrowAsync<TenantEntitlementException>();
        ex.Which.Code.Should().Be(EntitlementErrorCodes.EntitlementUnavailable);
    }

    private static TenantEntitlementSnapshot OperationalSnapshot(
        int maxActiveProducts = 500,
        IEnumerable<string>? features = null) =>
        TenantEntitlementSnapshot.Create(
            "t1",
            PlanCode.Starter,
            TenantSubscriptionState.Active,
            ManualInvoiceStatus.None,
            tenantActive: true,
            trialEndsAt: null,
            maxBrands: 2,
            maxActiveProducts: maxActiveProducts,
            features ?? ["catalog.write"],
            version: 1);

    private sealed class FakeTenantEntitlementStore(TenantEntitlementSnapshot? snapshot) : ITenantEntitlementStore
    {
        public Task<TenantEntitlementSnapshot?> TryGetAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task PublishAsync(TenantEntitlementSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<long> BumpVersionAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
    }
}
