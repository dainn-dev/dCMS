using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// DAI-679 orchestrator. Loads active campaigns (Redis-cached), runs registered
/// <see cref="IMechanicEvaluator"/> strategies in order, and aggregates results.
/// Promo-code resolution is delegated to <see cref="PromoCodeResolver"/> when present.
/// </summary>
public sealed class PromotionEvaluator : IPromotionEvaluator
{
    private readonly ICampaignPersistence _campaigns;
    private readonly ActiveCampaignsCache _cache;
    private readonly IReadOnlyDictionary<string, IMechanicEvaluator> _mechanics;
    private readonly PromoCodeResolver? _promoCodeResolver;
    private readonly TimeProvider _clock;

    public PromotionEvaluator(
        ICampaignPersistence campaigns,
        ActiveCampaignsCache cache,
        IEnumerable<IMechanicEvaluator> mechanics,
        TimeProvider clock,
        PromoCodeResolver? promoCodeResolver = null)
    {
        _campaigns = campaigns;
        _cache = cache;
        _mechanics = mechanics.ToDictionary(m => m.EditorKind, StringComparer.OrdinalIgnoreCase);
        _clock = clock;
        _promoCodeResolver = promoCodeResolver;
    }

    public async Task<EvaluateResponse> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
    {
        var ctx = new EvaluationContext(request);
        var now = _clock.GetUtcNow();

        RejectedCode? rejection = null;
        PromoCodeContext? codeCtx = null;
        if (!string.IsNullOrWhiteSpace(request.PromoCode) && _promoCodeResolver is not null)
        {
            var result = await _promoCodeResolver.ResolveAsync(request, now, ct).ConfigureAwait(false);
            rejection = result.Rejection;
            codeCtx = result.Code;
        }

        var campaigns = await LoadActiveAsync(request.TenantId, now, ct).ConfigureAwait(false);

        // If a promo code resolved successfully and is bound to a single campaign, restrict to it.
        if (codeCtx is { CampaignId: { Length: > 0 } cid })
            campaigns = campaigns.Where(c => string.Equals(c.Id, cid, StringComparison.Ordinal)).ToList();

        foreach (var campaign in campaigns.OrderBy(c => c.CreatedAt))
        {
            if (!_mechanics.TryGetValue(campaign.EditorKind, out var mechanic)) continue;
            mechanic.Evaluate(ctx, campaign);
        }

        // Stamp promo code on applied promotions when present.
        IReadOnlyList<AppliedPromotion> applied = ctx.AppliedPromotions;
        if (codeCtx is not null)
            applied = ctx.AppliedPromotions
                .Select(a => a with { PromoCode = codeCtx.Code })
                .ToList();

        return new EvaluateResponse(
            ctx.LineAdjustments,
            ctx.OrderAdjustments,
            applied,
            rejection,
            ctx.Suggestions.Count == 0 ? null : ctx.Suggestions,
            ctx.IssuedCodes.Count == 0 ? null : ctx.IssuedCodes);
    }

    private async Task<IReadOnlyList<CampaignRow>> LoadActiveAsync(string tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var cached = await _cache.TryGetAsync(tenantId, now, ct).ConfigureAwait(false);
        if (cached is not null) return cached;
        var fresh = await _campaigns.GetActiveByTenantAsync(tenantId, now, ct).ConfigureAwait(false);
        await _cache.SetAsync(tenantId, now, fresh, ct).ConfigureAwait(false);
        return fresh;
    }
}
