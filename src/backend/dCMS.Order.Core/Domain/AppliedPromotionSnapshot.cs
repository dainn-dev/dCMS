namespace dCMS.Order.Core.Domain;

/// <summary>
/// DAI-725 — Snapshot of a promotion that was applied to an order at create time.
/// Persisted in <c>OrderPromotions</c> for audit and to drive saga side-effects (Confirm/Release on Promotions.Api).
/// </summary>
public sealed record AppliedPromotionSnapshot(
    string Id,
    string CampaignId,
    string EditorKind,
    string Name,
    decimal Amount,
    string? PromoCode);
