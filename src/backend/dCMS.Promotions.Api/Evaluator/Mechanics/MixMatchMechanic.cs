using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator.Mechanics;

/// <summary>
/// EditorKind: mix-match. Three logic modes:
///   - "single-item": pick cheapest qualifying unit, apply discount to it.
///   - "per-bundle": qualifying units form bundles of size <c>bundleSize</c>; apply discount per complete bundle.
///   - "incremental": tiered table — first N qualifying units get discount X, next M get discount Y, etc.
/// MechanicsJson schema (subset):
///   { mode: "single-item"|"per-bundle"|"incremental",
///     discountType: "percent"|"fixed", discountValue: number,
///     bundleSize?: number, tiers?: [ { minQty: number, discountValue: number, discountType?: string } ] }
/// </summary>
public sealed class MixMatchMechanic : IMechanicEvaluator
{
    public string EditorKind => "mix-match";

    public void Evaluate(EvaluationContext ctx, CampaignRow campaign)
    {
        if (string.IsNullOrWhiteSpace(campaign.MechanicsJson)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(campaign.MechanicsJson); }
        catch (JsonException) { return; }
        using (doc)
        {
            var root = doc.RootElement;
            var mode = DiscountMath.ReadString(root, "mode", "single-item").ToLowerInvariant();

            var matching = QualifierMatcher.MatchingLines(campaign.QualifiersJson, ctx.Request.Lines);
            if (matching.Count == 0) return;

            switch (mode)
            {
                case "single-item":   ApplySingleItem(ctx, campaign, root, matching); break;
                case "per-bundle":    ApplyPerBundle(ctx, campaign, root, matching); break;
                case "incremental":   ApplyIncremental(ctx, campaign, root, matching); break;
            }
        }
    }

    private void ApplySingleItem(EvaluationContext ctx, CampaignRow campaign, JsonElement root, List<CartLine> matching)
    {
        var discountType = DiscountMath.ReadString(root, "discountType", "percent");
        var discountValue = DiscountMath.ReadDecimal(root, "discountValue");
        if (discountValue <= 0m) return;

        var cheapest = matching.OrderBy(l => l.UnitPrice).First();
        var headroom = ctx.RemainingOnLine(cheapest);
        if (headroom <= 0m) return;

        var discount = DiscountMath.Compute(discountType, discountValue, cheapest.UnitPrice);
        if (discount > headroom) discount = headroom;
        if (discount <= 0m) return;

        ctx.AddLineDiscount(cheapest.LineId, campaign.Id, discount, "mix-match:single-item");
        ctx.AppliedPromotions.Add(new AppliedPromotion(campaign.Id, EditorKind, campaign.Code, discount));
    }

    private void ApplyPerBundle(EvaluationContext ctx, CampaignRow campaign, JsonElement root, List<CartLine> matching)
    {
        var discountType = DiscountMath.ReadString(root, "discountType", "percent");
        var discountValue = DiscountMath.ReadDecimal(root, "discountValue");
        var bundleSize = DiscountMath.ReadInt(root, "bundleSize", 0);
        if (discountValue <= 0m || bundleSize <= 0) return;

        var totalQty = matching.Sum(l => l.Quantity);
        var bundles = totalQty / bundleSize;
        if (bundles <= 0) return;

        // Discount the cheapest "bundles * bundleSize" units (line-by-line, ascending price).
        var unitsToDiscount = bundles * bundleSize;
        var ordered = matching.OrderBy(l => l.UnitPrice).ToList();
        decimal totalApplied = 0m;
        foreach (var line in ordered)
        {
            if (unitsToDiscount <= 0) break;
            var units = Math.Min(line.Quantity, unitsToDiscount);
            var basePrice = line.UnitPrice * units;
            var headroom = ctx.RemainingOnLine(line);
            if (headroom <= 0m) { unitsToDiscount -= units; continue; }

            var discount = DiscountMath.Compute(discountType, discountValue, basePrice);
            if (discount > headroom) discount = headroom;
            if (discount > 0m)
            {
                ctx.AddLineDiscount(line.LineId, campaign.Id, discount, $"mix-match:per-bundle:{bundleSize}");
                totalApplied += discount;
            }
            unitsToDiscount -= units;
        }
        if (totalApplied > 0m)
            ctx.AppliedPromotions.Add(new AppliedPromotion(campaign.Id, EditorKind, campaign.Code, totalApplied));
    }

    private void ApplyIncremental(EvaluationContext ctx, CampaignRow campaign, JsonElement root, List<CartLine> matching)
    {
        if (!root.TryGetProperty("tiers", out var tiersEl) || tiersEl.ValueKind != JsonValueKind.Array) return;

        var totalQty = matching.Sum(l => l.Quantity);

        // Pick the highest-minQty tier the cart hits.
        Tier? best = null;
        foreach (var t in tiersEl.EnumerateArray())
        {
            var minQty = DiscountMath.ReadInt(t, "minQty", 0);
            if (minQty <= 0 || totalQty < minQty) continue;
            var dv = DiscountMath.ReadDecimal(t, "discountValue");
            var dt = DiscountMath.ReadString(t, "discountType", DiscountMath.ReadString(root, "discountType", "percent"));
            if (dv <= 0m) continue;
            if (best is null || minQty > best.Value.MinQty)
                best = new Tier(minQty, dt, dv);
        }
        if (best is null) return;

        // Apply tier discount to all matching units (cheapest-first ordering for stable tests).
        var ordered = matching.OrderBy(l => l.UnitPrice).ToList();
        decimal totalApplied = 0m;
        foreach (var line in ordered)
        {
            var headroom = ctx.RemainingOnLine(line);
            if (headroom <= 0m) continue;
            var basePrice = line.UnitPrice * line.Quantity;
            var discount = DiscountMath.Compute(best.Value.DiscountType, best.Value.DiscountValue, basePrice);
            if (discount > headroom) discount = headroom;
            if (discount <= 0m) continue;
            ctx.AddLineDiscount(line.LineId, campaign.Id, discount, $"mix-match:incremental:tier{best.Value.MinQty}");
            totalApplied += discount;
        }
        if (totalApplied > 0m)
            ctx.AppliedPromotions.Add(new AppliedPromotion(campaign.Id, EditorKind, campaign.Code, totalApplied));
    }

    private readonly record struct Tier(int MinQty, string DiscountType, decimal DiscountValue);
}
