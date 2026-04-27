using dCMS.Promotions.Api.Evaluator;
using dCMS.Promotions.Api.Evaluator.Mechanics;
using dCMS.Promotions.Contracts.Evaluate;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Promotions.Mechanics;

public sealed class PwpMechanicTests
{
    private static EvaluationContext NewContext(params CartLine[] lines) =>
        new(new EvaluateRequest(
            TenantId: PromotionFixtures.TenantId,
            StoreId: null, CustomerId: null, PromoCode: null,
            Currency: "VND", Lines: lines,
            OrderSubtotal: lines.Sum(l => l.UnitPrice * l.Quantity),
            IdempotencyKey: Guid.NewGuid().ToString()));

    // ── pwp-item ──────────────────────────────────────────────────────────────

    [Fact]
    public void PwpItem_emits_suggestions_when_qualifying_threshold_met()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "Q1", 100m, qty: 4));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "pwp-item",
            PromotionFixtures.PwpItemJson(qualifyingPerSet: 2, maxPerUser: 5, "GIFT1", "GIFT2", "GIFT3"),
            PromotionFixtures.QualifyByProducts("Q1"));

        new PwpItemMechanic().Evaluate(ctx, camp);

        // 4/2 = 2 sets, capped to 3 promo products available = 2 emitted
        ctx.Suggestions.Should().HaveCount(2);
        ctx.Suggestions.Select(s => s.ProductId).Should().Equal("GIFT1", "GIFT2");
        ctx.LineAdjustments.Should().BeEmpty();
    }

    [Fact]
    public void PwpItem_no_suggestions_below_threshold()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "Q1", 100m, qty: 1));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "pwp-item",
            PromotionFixtures.PwpItemJson(qualifyingPerSet: 2, maxPerUser: 5, "GIFT1"),
            PromotionFixtures.QualifyByProducts("Q1"));

        new PwpItemMechanic().Evaluate(ctx, camp);

        ctx.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public void PwpItem_suggestions_capped_by_maxPerUser()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "Q1", 100m, qty: 10));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "pwp-item",
            PromotionFixtures.PwpItemJson(qualifyingPerSet: 1, maxPerUser: 2, "G1", "G2", "G3", "G4"),
            PromotionFixtures.QualifyByProducts("Q1"));

        new PwpItemMechanic().Evaluate(ctx, camp);

        ctx.Suggestions.Should().HaveCount(2);
    }

    // ── pwp-discount ──────────────────────────────────────────────────────────

    [Fact]
    public void PwpDiscount_applies_to_promotional_lines_when_qualifier_met()
    {
        var ctx = NewContext(
            PromotionFixtures.Line("L1", "Q1", 100m, qty: 2),
            PromotionFixtures.Line("L2", "GIFT1", 50m, qty: 1));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "pwp-discount",
            PromotionFixtures.PwpDiscountJson(2, 5, "percent", 50m, "GIFT1"),
            PromotionFixtures.QualifyByProducts("Q1"));

        new PwpDiscountMechanic().Evaluate(ctx, camp);

        ctx.LineAdjustments.Single().LineId.Should().Be("L2");
        ctx.LineAdjustments.Single().Amount.Should().Be(25m); // 50 * 50%
    }

    [Fact]
    public void PwpDiscount_no_op_when_promo_product_absent_from_cart()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "Q1", 100m, qty: 5));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "pwp-discount",
            PromotionFixtures.PwpDiscountJson(2, 5, "percent", 50m, "GIFT1"),
            PromotionFixtures.QualifyByProducts("Q1"));

        new PwpDiscountMechanic().Evaluate(ctx, camp);

        ctx.LineAdjustments.Should().BeEmpty();
    }

    // ── after-sales ───────────────────────────────────────────────────────────

    [Fact]
    public void AfterSales_issues_promised_codes_when_threshold_met()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "Q1", 100m, qty: 6));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "after-sales",
            PromotionFixtures.AfterSalesJson(qualifyingInCart: 2, maxSets: 5, codeType: "group"),
            PromotionFixtures.QualifyByProducts("Q1"));

        new AfterSalesMechanic().Evaluate(ctx, camp);

        var promise = ctx.IssuedCodes.Should().ContainSingle().Which;
        promise.Count.Should().Be(3); // 6/2 sets
        promise.CodeType.Should().Be("group");
    }

    [Fact]
    public void AfterSales_no_issuance_below_threshold()
    {
        var ctx = NewContext(PromotionFixtures.Line("L1", "Q1", 100m, qty: 1));
        var camp = PromotionFixtures.ActiveCampaign(
            "C1", "after-sales",
            PromotionFixtures.AfterSalesJson(qualifyingInCart: 2, maxSets: 5),
            PromotionFixtures.QualifyByProducts("Q1"));

        new AfterSalesMechanic().Evaluate(ctx, camp);

        ctx.IssuedCodes.Should().BeEmpty();
    }
}
