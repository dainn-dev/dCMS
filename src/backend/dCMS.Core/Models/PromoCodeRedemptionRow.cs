namespace dCMS.Core.Models;

/// <summary>
/// DAI-692: tracks promo code redemptions for cap enforcement, idempotency, and release on cancel.
/// </summary>
public sealed record PromoCodeRedemptionRow(
    string Id,
    string TenantId,
    string PromoCodeId,
    string OrderId,
    string? CustomerId,
    string? GroupId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset RedeemedAt,
    DateTimeOffset? ReleasedAt);
