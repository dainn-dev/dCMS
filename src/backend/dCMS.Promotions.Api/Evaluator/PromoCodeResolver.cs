using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Resolved promo-code context: the code matched, what campaign it binds to, and any per-line exclusions.
/// </summary>
public sealed record PromoCodeContext(
    string PromoCodeId,
    string Code,
    string? CampaignId,
    IReadOnlySet<string>? ExcludedProductIds);

public sealed record PromoCodeResolveResult(PromoCodeContext? Code, RejectedCode? Rejection);

/// <summary>
/// DAI-692: validates a promo code against status, validity window, customer binding,
/// per-customer + total caps, group exclusivity, and exclusion list.
/// Implemented in DAI-692 step; declared here so <see cref="PromotionEvaluator"/> can wire optionally.
/// </summary>
public abstract class PromoCodeResolver
{
    public abstract Task<PromoCodeResolveResult> ResolveAsync(
        EvaluateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
