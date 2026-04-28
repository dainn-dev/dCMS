using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator.Mechanics;

/// <summary>
/// EditorKind: pwp-discount.
/// MechanicsJson schema (subset):
///   { qualifyingProductsPerSet: number,
///     maxPromotionalProductsPerUser?: number,
///     promotionProductIds: string[],
///     discountType: "percent"|"fixed", discountMode: "add"|"override",
///     discountValue: number }
/// When qualifying lines hit <c>qualifyingProductsPerSet</c>, applies the discount to lines whose
/// productId is in <c>promotionProductIds</c>, capped at <c>maxPromotionalProductsPerUser</c> sets.
/// </summary>
public sealed class PwpDiscountMechanic : IMechanicEvaluator
{
    public string EditorKind => "pwp-discount";

    public void Evaluate(EvaluationContext ctx, CampaignRow campaign)
    {
        if (string.IsNullOrWhiteSpace(campaign.MechanicsJson)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(campaign.MechanicsJson); }
        catch (JsonException) { return; }
        using (doc)
        {
            var root = doc.RootElement;
            var perSet = DiscountMath.ReadInt(root, "qualifyingProductsPerSet", 0);
            var maxPerUser = DiscountMath.ReadInt(root, "maxPromotionalProductsPerUser", int.MaxValue);
            var discountType = DiscountMath.ReadString(root, "discountType", "percent");
            var discountValue = DiscountMath.ReadDecimal(root, "discountValue");
            if (perSet <= 0 || maxPerUser <= 0 || discountValue <= 0m) return;

            var matching = QualifierMatcher.MatchingLines(campaign.QualifiersJson, ctx.Request.Lines);
            var qualifyingQty = matching.Sum(l => l.Quantity);
            if (qualifyingQty < perSet) return;

            var sets = qualifyingQty / perSet;
            var allowedUnits = Math.Min(sets, maxPerUser);
            if (allowedUnits <= 0) return;

            var promoIds = ReadStringSet(root, "promotionProductIds");
            if (promoIds is null || promoIds.Count == 0) return;

            var promoLines = ctx.Request.Lines
                .Where(l => promoIds.Contains(l.ProductId))
                .OrderBy(l => l.UnitPrice)
                .ToList();
            if (promoLines.Count == 0) return;

            decimal totalApplied = 0m;
            foreach (var line in promoLines)
            {
                if (allowedUnits <= 0) break;
                var headroom = ctx.RemainingOnLine(line);
                if (headroom <= 0m) continue;

                var units = Math.Min(line.Quantity, allowedUnits);
                var basePrice = line.UnitPrice * units;
                var discount = DiscountMath.Compute(discountType, discountValue, basePrice);
                if (discount > headroom) discount = headroom;
                if (discount <= 0m) { allowedUnits -= units; continue; }

                ctx.AddLineDiscount(line.LineId, campaign.Id, discount, $"pwp-discount:{discountType}");
                totalApplied += discount;
                allowedUnits -= units;
            }

            if (totalApplied > 0m)
                ctx.AppliedPromotions.Add(new AppliedPromotion(campaign.Id, EditorKind, campaign.Code, totalApplied));
        }
    }

    private static HashSet<string>? ReadStringSet(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var el in arr.EnumerateArray())
            if (el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } s) set.Add(s);
        return set.Count == 0 ? null : set;
    }
}
