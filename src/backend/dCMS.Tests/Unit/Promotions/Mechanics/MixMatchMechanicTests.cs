using dCMS.Promotions.Api.Evaluator;
using dCMS.Promotions.Api.Evaluator.Mechanics;
using dCMS.Promotions.Contracts.Evaluate;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Promotions.Mechanics;

public sealed class MixMatchMechanicTests
{
    private readonly MixMatchMechanic _mechanic = new();

    private EvaluationContext NewContext(params CartLine[] lines)
    {
        var req = new EvaluateRequest(
            TenantId: PromotionFixtures.TenantId,
            StoreId: null,
            CustomerId: null,
            PromoCode: null,
            Currency: "VND",
            Lines: lines,
            OrderSubtotal: lines.Sum(l => l.UnitPrice * l.Quantity),
            IdempotencyKey: Guid.NewGuid().ToString());
        return new EvaluationContext(req);
    }

    [Fact]
    public void SingleItem_picks_cheapest_qualifying_line()
    {
        var ctx = NewContext(
            PromotionFixtures.Line("L1", "P1", 200m),
            PromotionFixtures.Line("L2", "P2", 50m),
            PromotionFixtures.Line("L3", "P3", 100m));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "mix-match",
            PromotionFixtures.MixMatchSingleItem("percent", 50m),
            PromotionFixtures.QualifyByProducts("P1", "P2", "P3"));

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().ContainSingle()
            .Which.LineId.Should().Be("L2");
        ctx.LineAdjustments.Single().Amount.Should().Be(25m); // 50 * 50%
    }

    [Fact]
    public void PerBundle_discounts_cheapest_units_for_complete_bundles()
    {
        var ctx = NewContext(
            PromotionFixtures.Line("L1", "P1", 100m, qty: 1),
            PromotionFixtures.Line("L2", "P2", 50m, qty: 4));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "mix-match",
            PromotionFixtures.MixMatchPerBundle("fixed", 10m, bundleSize: 3),
            PromotionFixtures.QualifyByProducts("P1", "P2"));

        // Total qty 5, bundleSize 3 → 1 complete bundle covering 3 cheapest units of L2
        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().HaveCount(1);
        var adj = ctx.LineAdjustments.Single();
        adj.LineId.Should().Be("L2");
        adj.Amount.Should().Be(10m); // fixed 10 on the bundle
    }

    [Fact]
    public void PerBundle_no_discount_when_below_bundleSize()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "P1", 100m, qty: 2));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "mix-match",
            PromotionFixtures.MixMatchPerBundle("percent", 20m, bundleSize: 3),
            PromotionFixtures.QualifyByProducts("P1"));

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().BeEmpty();
    }

    [Fact]
    public void Incremental_picks_highest_tier_matching_total_qty()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "P1", 100m, qty: 5));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "mix-match",
            PromotionFixtures.MixMatchIncremental("percent", (3, 10m), (5, 25m), (10, 50m)),
            PromotionFixtures.QualifyByProducts("P1"));

        _mechanic.Evaluate(ctx, camp);

        // Hits tier minQty=5, 25% on 500 = 125
        ctx.LineAdjustments.Single().Amount.Should().Be(125m);
    }

    [Fact]
    public void Incremental_no_tier_hit_yields_no_discount()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "P1", 100m, qty: 2));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "mix-match",
            PromotionFixtures.MixMatchIncremental("percent", (3, 10m), (5, 25m)),
            PromotionFixtures.QualifyByProducts("P1"));

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().BeEmpty();
    }

    [Fact]
    public void Empty_qualifiers_match_all_lines()
    {
        var ctx = NewContext(
            PromotionFixtures.Line("L1", "P1", 30m),
            PromotionFixtures.Line("L2", "P2", 10m));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "mix-match",
            PromotionFixtures.MixMatchSingleItem("fixed", 5m),
            qualifiersJson: "{}");

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Single().LineId.Should().Be("L2");
        ctx.LineAdjustments.Single().Amount.Should().Be(5m);
    }
}
