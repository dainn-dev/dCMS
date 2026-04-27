using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator.Mechanics;

/// <summary>
/// EditorKind: pwp-item ("purchase-with-purchase").
/// MechanicsJson schema (subset):
///   { qualifyingProductsPerSet: number,
///     maxPromotionalProductsPerUser?: number,
///     promotionProductIds: string[] }
/// When qualifying lines hit <c>qualifyingProductsPerSet</c>, surfaces the promotion products
/// as <see cref="Suggestion"/>s (not auto-added) up to the per-user cap.
/// </summary>
public sealed class PwpItemMechanic : IMechanicEvaluator
{
    public string EditorKind => "pwp-item";

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
            if (perSet <= 0 || maxPerUser <= 0) return;

            var matching = QualifierMatcher.MatchingLines(campaign.QualifiersJson, ctx.Request.Lines);
            var qualifyingQty = matching.Sum(l => l.Quantity);
            if (qualifyingQty < perSet) return;

            var sets = qualifyingQty / perSet;
            var available = Math.Min(sets, maxPerUser);
            if (available <= 0) return;

            if (!root.TryGetProperty("promotionProductIds", out var idsEl) ||
                idsEl.ValueKind != JsonValueKind.Array) return;

            var emitted = 0;
            foreach (var el in idsEl.EnumerateArray())
            {
                if (emitted >= available) break;
                if (el.ValueKind != JsonValueKind.String) continue;
                var pid = el.GetString();
                if (string.IsNullOrEmpty(pid)) continue;
                ctx.Suggestions.Add(new Suggestion(pid!, campaign.Id, "pwp-item"));
                emitted++;
            }

            if (emitted > 0)
                ctx.AppliedPromotions.Add(new AppliedPromotion(campaign.Id, EditorKind, campaign.Code, 0m));
        }
    }
}
