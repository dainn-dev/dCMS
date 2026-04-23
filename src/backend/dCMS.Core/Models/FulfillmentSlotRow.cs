namespace dCMS.Core.Models;

/// <summary>Time / option slot under a <see cref="FulfillmentGroupingRow"/>.</summary>
public sealed record FulfillmentSlotRow(
    string    Id,
    string    TenantId,
    string    GroupingId,
    string    Name,
    string    Code,
    string    Mode,
    DateOnly  StartingDate,
    DateOnly  EndingDate,
    string    Price,
    DateTimeOffset UpdatedAt)
{
    public static bool IsValidCode(string code) => FulfillmentGroupingRow.IsValidCode(code);
}
