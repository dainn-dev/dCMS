namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record AppliedPromotion(
    string CampaignId,
    string EditorKind,
    string Name,
    decimal Amount,
    string? PromoCode = null);
