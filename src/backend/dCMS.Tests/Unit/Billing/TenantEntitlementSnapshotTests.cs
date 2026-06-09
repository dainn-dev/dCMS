using dCMS.Billing.Domain;
using FluentAssertions;

namespace dCMS.Tests.Unit.Billing;

public sealed class TenantEntitlementSnapshotTests
{
    [Fact]
    public void Active_tenant_is_operational()
    {
        var snapshot = Build(TenantSubscriptionState.Active, tenantActive: true);
        snapshot.IsOperational.Should().BeTrue();
        snapshot.ResolveOperationalReason().Should().BeNull();
    }

    [Fact]
    public void Trial_before_expiry_is_operational()
    {
        var snapshot = Build(
            TenantSubscriptionState.Trial,
            tenantActive: true,
            trialEndsAt: DateTimeOffset.UtcNow.AddDays(7));
        snapshot.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void Trial_after_expiry_is_not_operational()
    {
        var snapshot = Build(
            TenantSubscriptionState.Trial,
            tenantActive: true,
            trialEndsAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        snapshot.IsOperational.Should().BeFalse();
        snapshot.ResolveOperationalReason().Should().Be(EntitlementErrorCodes.TrialExpired);
    }

    [Fact]
    public void Suspended_is_not_operational()
    {
        var snapshot = Build(TenantSubscriptionState.Suspended, tenantActive: true);
        snapshot.ResolveOperationalReason().Should().Be(EntitlementErrorCodes.SubscriptionSuspended);
    }

    [Fact]
    public void Cancelled_is_not_operational()
    {
        var snapshot = Build(TenantSubscriptionState.Cancelled, tenantActive: true);
        snapshot.ResolveOperationalReason().Should().Be(EntitlementErrorCodes.SubscriptionCancelled);
    }

    [Fact]
    public void Inactive_tenant_is_not_operational_even_when_active_subscription()
    {
        var snapshot = Build(TenantSubscriptionState.Active, tenantActive: false);
        snapshot.ResolveOperationalReason().Should().Be(EntitlementErrorCodes.TenantInactive);
    }

    private static TenantEntitlementSnapshot Build(
        TenantSubscriptionState state,
        bool tenantActive,
        DateTimeOffset? trialEndsAt = null) =>
        TenantEntitlementSnapshot.Create(
            "t1",
            PlanCode.Starter,
            state,
            ManualInvoiceStatus.None,
            tenantActive,
            trialEndsAt,
            maxBrands: 2,
            maxActiveProducts: 500,
            ["catalog.write"],
            version: 1);
}
