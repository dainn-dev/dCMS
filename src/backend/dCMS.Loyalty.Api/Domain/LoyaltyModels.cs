namespace dCMS.Loyalty.Api.Domain;

public sealed record LoyaltyHoldRow(
    Guid Id,
    string TenantId,
    string CustomerId,
    Guid OrderId,
    decimal Amount,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoyaltyLedgerEntry(
    long Id,
    string TenantId,
    string CustomerId,
    decimal Delta,
    string Reason,
    Guid? OrderId,
    Guid? HoldId,
    string? Notes,
    DateTimeOffset OccurredAt);

public static class LoyaltyHoldStatus
{
    public const string Held = "Held";
    public const string Captured = "Captured";
    public const string Released = "Released";
    public const string Refunded = "Refunded";
}

public static class LoyaltyLedgerReason
{
    public const string Earn = "EARN";
    public const string Spend = "SPEND";
    public const string Refund = "REFUND";
    public const string Adjust = "ADJUST";
}
