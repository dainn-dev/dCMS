namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record EvaluateResponse(
    IReadOnlyList<LineAdjustment> LineAdjustments,
    IReadOnlyList<OrderAdjustment> OrderAdjustments,
    IReadOnlyList<AppliedPromotion> AppliedPromotions,
    RejectedCode? RejectedCode = null,
    IReadOnlyList<Suggestion>? Suggestions = null,
    IReadOnlyList<IssuedCodePromise>? IssuedCodes = null,
    string? PromoCodeId = null)
{
    public static EvaluateResponse Empty => new(
        Array.Empty<LineAdjustment>(),
        Array.Empty<OrderAdjustment>(),
        Array.Empty<AppliedPromotion>());
}
