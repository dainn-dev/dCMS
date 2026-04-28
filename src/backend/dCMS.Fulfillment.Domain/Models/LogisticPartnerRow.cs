namespace dCMS.Core.Models;

public sealed record LogisticPartnerRow(
    string    Id,
    string    TenantId,
    string    Name,
    string    Code,
    bool      Enabled,
    bool      IntegratedLogistic,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static bool IsValidCode(string code) => FulfillmentGroupingRow.IsValidCode(code);
}
