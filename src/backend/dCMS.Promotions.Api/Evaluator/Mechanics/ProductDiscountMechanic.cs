using System.Text.Json;
using dCMS.Core.Models;

namespace dCMS.Promotions.Api.Evaluator.Mechanics;

/// <summary>
/// EditorKind: product-discount.
/// MechanicsJson schema: { discountType: "percent"|"fixed", discountMode: "add"|"override",
///                         discountValue: number, qualifyingProductsToAvail?: number }.
/// Applies discount per qualifying line up to <c>qualifyingProductsToAvail</c> units across the cart.
/// </summary>
public sealed class ProductDiscountMechanic : IMechanicEvaluator
{
    public string EditorKind => "product-discount";

    public void Evaluate(EvaluationContext ctx, CampaignRow campaign)
    {
        if (string.IsNullOrWhiteSpace(campaign.MechanicsJson)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(campaign.MechanicsJson); }
        catch (JsonException) { return; }
        using (doc)
        {
            var root = doc.RootElement;
            var discountType = DiscountMath.ReadString(root, "discountType", "percent");
            var discountValue = DiscountMath.ReadDecimal(root, "discountValue");
            var cap = DiscountMath.ReadInt(root, "qualifyingProductsToAvail", int.MaxValue);
            if (discountValue <= 0m || cap <= 0) return;

            var matching = QualifierMatcher.MatchingLines(campaign.QualifiersJson, ctx.Request.Lines);
            if (matching.Count == 0) return;

            var unitsRemaining = cap;
            var name = campaign.Code;
            foreach (var line in matching)
            {
                if (unitsRemaining <= 0) break;
                var unitsToDiscount = Math.Min(line.Quantity, unitsRemaining);
                var basePrice = line.UnitPrice * unitsToDiscount;
                var headroom = ctx.RemainingOnLine(line);
                if (headroom <= 0m) continue;

                var discount = DiscountMath.Compute(discountType, discountValue, basePrice);
                if (discount > headroom) discount = headroom;
                if (discount <= 0m) continue;

                ctx.AddLineDiscount(line.LineId, campaign.Id, discount, $"product-discount:{discountType}");
                ctx.AppliedPromotions.Add(new Contracts.Evaluate.AppliedPromotion(
                    campaign.Id, EditorKind, name, discount));
                unitsRemaining -= unitsToDiscount;
            }
        }
    }
}
