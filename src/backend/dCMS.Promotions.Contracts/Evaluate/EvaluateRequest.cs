namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record EvaluateRequest(
    string TenantId,
    string? StoreId,
    string? CustomerId,
    string? PromoCode,
    string Currency,
    IReadOnlyList<CartLine> Lines,
    decimal OrderSubtotal,
    string IdempotencyKey);
