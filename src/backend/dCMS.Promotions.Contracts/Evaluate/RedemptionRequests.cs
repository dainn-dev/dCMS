namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record ConfirmRedemptionRequest(
    string OrderId,
    string PromoCodeId,
    string? CustomerId,
    decimal Amount,
    string Currency);

public sealed record ReleaseRedemptionRequest(string OrderId);
