using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator.Mechanics;

/// <summary>
/// EditorKind: after-sales.
/// MechanicsJson schema (subset):
///   { qualifyingProductsInCart: number,
///     promotionCodeType: "standard"|"group",
///     maxDiscountedProductSets?: number,
///     maxRewardPerOrder?: number }
/// When qualifying-product threshold is met, signals the count of promo codes to be
/// issued post-purchase via <see cref="IssuedCodePromise"/>. Actual issuance happens
/// when Order.Api confirms the order.
/// </summary>
public sealed class AfterSalesMechanic : IMechanicEvaluator
{
    public string EditorKind => "after-sales";

    public void Evaluate(EvaluationContext ctx, CampaignRow campaign)
    {
        if (string.IsNullOrWhiteSpace(campaign.MechanicsJson)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(campaign.MechanicsJson); }
        catch (JsonException) { return; }
        using (doc)
        {
            var root = doc.RootElement;
            var inCart = DiscountMath.ReadInt(root, "qualifyingProductsInCart", 0);
            var maxSets = DiscountMath.ReadInt(root, "maxDiscountedProductSets", int.MaxValue);
            var maxReward = DiscountMath.ReadInt(root, "maxRewardPerOrder", int.MaxValue);
            var codeType = DiscountMath.ReadString(root, "promotionCodeType", "standard").ToLowerInvariant();
            if (inCart <= 0) return;

            var matching = QualifierMatcher.MatchingLines(campaign.QualifiersJson, ctx.Request.Lines);
            var qualifyingQty = matching.Sum(l => l.Quantity);
            if (qualifyingQty < inCart) return;

            var sets = qualifyingQty / inCart;
            var count = Math.Min(Math.Min(sets, maxSets), maxReward);
            if (count <= 0) return;

            ctx.IssuedCodes.Add(new IssuedCodePromise(campaign.Id, count, codeType));
            ctx.AppliedPromotions.Add(new AppliedPromotion(campaign.Id, EditorKind, campaign.Code, 0m));
        }
    }
}
