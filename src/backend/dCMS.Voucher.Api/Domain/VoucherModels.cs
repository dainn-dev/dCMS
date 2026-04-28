namespace dCMS.Voucher.Api.Domain;

public sealed record VoucherRow(
    Guid Id,
    string TenantId,
    string Code,
    decimal FaceValue,
    decimal RemainingValue,
    string Currency,
    string Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record VoucherHoldRow(
    Guid Id,
    string TenantId,
    Guid VoucherId,
    Guid OrderId,
    decimal Amount,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class VoucherStatus
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";
    public const string Expired = "Expired";
    public const string Consumed = "Consumed";
}

public static class VoucherHoldStatus
{
    public const string Held = "Held";
    public const string Captured = "Captured";
    public const string Released = "Released";
    public const string Refunded = "Refunded";
}
