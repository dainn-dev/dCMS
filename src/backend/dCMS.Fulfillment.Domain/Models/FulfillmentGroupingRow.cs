using System.Text.RegularExpressions;

namespace dCMS.Core.Models;

/// <summary>Tenant-level fulfillment option group (DAI-612).</summary>
public sealed record FulfillmentGroupingRow(
    string    Id,
    string    TenantId,
    string    GroupName,
    string    Code,
    DateOnly  StartDate,
    DateOnly  EndDate,
    int       Priority,
    bool      Active,
    bool      TenantEnabled,
    int?      MaxPerTenant,
    string    DeliveryMode,
    bool      LimitSelectedDistributionCenter,
    string    StockLocation,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static readonly HashSet<string> ValidDeliveryModes =
    [
        "Store Collection",
        "Local Delivery",
        "Overseas Delivery",
    ];

    /// <summary>Upper-snake-style code, same rules as <see cref="CampaignRow.IsValidCode"/>.</summary>
    public static bool IsValidCode(string code) =>
        !string.IsNullOrWhiteSpace(code) &&
        code.Length <= 100 &&
        Regex.IsMatch(code, @"^[A-Z][A-Z0-9_]*$");
}
