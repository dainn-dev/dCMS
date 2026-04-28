using dCMS.Order.Core.Domain.Payments;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

public sealed class OrderPaymentTests
{
    [Fact]
    public void Plan_orders_components_voucher_loyalty_giftcard_gateway()
    {
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, total: 100m, tenders: new[]
        {
            (PaymentComponentType.Gateway, 50m),
            (PaymentComponentType.Voucher, 30m),
            (PaymentComponentType.LoyaltyPoints, 20m),
        });

        Assert.Equal(new[]
        {
            PaymentComponentType.Voucher,
            PaymentComponentType.LoyaltyPoints,
            PaymentComponentType.Gateway,
        }, plan.Components.Select(c => c.Type));
        Assert.Equal(new[] { 0, 1, 2 }, plan.Components.Select(c => c.Ordering));
        Assert.Equal("Pending", plan.Status);
    }

    [Fact]
    public void Plan_drops_zero_amount_tenders()
    {
        var plan = OrderPayment.Plan(Guid.NewGuid(), 50m, new[]
        {
            (PaymentComponentType.Voucher, 0m),
            (PaymentComponentType.Gateway, 50m),
        });
        Assert.Single(plan.Components);
        Assert.Equal(PaymentComponentType.Gateway, plan.Components[0].Type);
    }

    [Fact]
    public void EnsureSumInvariant_throws_when_sum_does_not_match_total()
    {
        var components = new[]
        {
            new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Gateway, 70m, 0),
        };
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new OrderPayment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Pending", components));
        Assert.Contains("sum", ex.Message);
        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public void EnsureSumInvariant_passes_when_components_equal_total()
    {
        var components = new[]
        {
            new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Voucher, 30m, 0),
            new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Gateway, 70m, 1),
        };
        var p = new OrderPayment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Pending", components);
        Assert.Equal(100m, p.Total);
    }

    [Fact]
    public void RecomputeStatus_Captured_when_all_components_captured()
    {
        var c1 = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Voucher, 30m, 0);
        var c2 = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Gateway, 70m, 1);
        c1.Capture(); c2.Capture();
        var p = new OrderPayment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Pending", new[] { c1, c2 });
        p.RecomputeStatus();
        Assert.Equal("Captured", p.Status);
    }

    [Fact]
    public void RecomputeStatus_Failed_if_any_component_failed()
    {
        var c1 = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Voucher, 30m, 0);
        var c2 = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Gateway, 70m, 1);
        c1.Capture(); c2.Fail("declined");
        var p = new OrderPayment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Pending", new[] { c1, c2 });
        p.RecomputeStatus();
        Assert.Equal("Failed", p.Status);
    }

    [Fact]
    public void PaymentComponent_rejects_negative_amount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Voucher, -1m, 0));
    }

    [Fact]
    public void PaymentComponent_state_transitions_track_timestamps()
    {
        var c = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Gateway, 50m, 0);
        Assert.Equal(PaymentComponentState.Pending, c.State);
        c.Authorize("ext-123");
        Assert.Equal(PaymentComponentState.Authorized, c.State);
        Assert.Equal("ext-123", c.ExternalRef);
        c.Capture();
        Assert.Equal(PaymentComponentState.Captured, c.State);
        Assert.NotNull(c.UpdatedAt);
    }

    [Fact]
    public void Plan_persists_reference_per_component()
    {
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.LoyaltyPoints, 60m, (string?)"cust-1"),
        });

        Assert.Equal("PROMO10", plan.Components.Single(c => c.Type == PaymentComponentType.Voucher).Reference);
        Assert.Equal("cust-1", plan.Components.Single(c => c.Type == PaymentComponentType.LoyaltyPoints).Reference);
        Assert.All(plan.Components, c => Assert.Null(c.ExternalRef));
    }

    [Fact]
    public void Authorize_sets_ExternalRef_without_clearing_Reference()
    {
        var c = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Voucher, 40m, ordering: 0,
            reference: "PROMO10");
        c.Authorize("hold-123");
        Assert.Equal("PROMO10", c.Reference);
        Assert.Equal("hold-123", c.ExternalRef);
    }
}
