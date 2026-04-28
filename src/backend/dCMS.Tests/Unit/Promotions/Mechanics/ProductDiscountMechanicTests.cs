using dCMS.Promotions.Api.Evaluator;
using dCMS.Promotions.Api.Evaluator.Mechanics;
using dCMS.Promotions.Contracts.Evaluate;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Promotions.Mechanics;

public sealed class ProductDiscountMechanicTests
{
    private readonly ProductDiscountMechanic _mechanic = new();

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
    public void Percent_discount_applies_to_qualifying_lines_capped_by_units()
    {
        var ctx = NewContext(
            PromotionFixtures.Line("L1", "P1", 100m, qty: 3),
            PromotionFixtures.Line("L2", "P2", 50m, qty: 2));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "product-discount",
            PromotionFixtures.ProductDiscountJson("percent", 10m, cap: 4),
            PromotionFixtures.QualifyByProducts("P1", "P2"));

        _mechanic.Evaluate(ctx, camp);

        // 3 units of P1 @100 + 1 unit of P2 @50 → cap reached at 4 units
        ctx.LineAdjustments.Should().HaveCount(2);
        ctx.LineAdjustments.Single(a => a.LineId == "L1").Amount.Should().Be(30m); // 100 * 3 * 10%
        ctx.LineAdjustments.Single(a => a.LineId == "L2").Amount.Should().Be(5m);  // 50 * 1 * 10%
    }

    [Fact]
    public void Fixed_discount_is_flat_amount_capped_at_line_total()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "P1", 100m, qty: 2));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "product-discount",
            PromotionFixtures.ProductDiscountJson("fixed", 5m),
            PromotionFixtures.QualifyByProducts("P1"));

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Single().Amount.Should().Be(5m);
    }

    [Fact]
    public void No_match_yields_no_adjustment()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "OTHER", 100m));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "product-discount",
            PromotionFixtures.ProductDiscountJson("percent", 50m),
            PromotionFixtures.QualifyByProducts("P1"));

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().BeEmpty();
        ctx.AppliedPromotions.Should().BeEmpty();
    }

    [Fact]
    public void Discount_clamped_to_line_remaining_headroom()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "P1", 10m, qty: 1));
        // Pre-discount the line so headroom is only 4
        ctx.AddLineDiscount("L1", "PRIOR", 6m, "prior");

        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "product-discount",
            PromotionFixtures.ProductDiscountJson("fixed", 100m),
            PromotionFixtures.QualifyByProducts("P1"));

        _mechanic.Evaluate(ctx, camp);

        // Total discount on L1 must not exceed 10
        ctx.AccumulatedLineDiscount["L1"].Should().Be(10m);
    }

    [Fact]
    public void Invalid_mechanics_json_is_ignored()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "P1", 100m));
        var camp = PromotionFixtures.ActiveCampaign("C1", "product-discount", "{not json");

        _mechanic.Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().BeEmpty();
    }
}
