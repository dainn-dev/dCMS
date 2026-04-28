namespace dCMS.Core.Models;

/// <summary>JSON blobs aligned with eStore localStorage: predefined fields, dynamic fields, stock locations.</summary>
public sealed record FulfillmentTenantSettingsRow(
    string    TenantId,
    string    PredefinedFieldsJson,
    string    DynamicFieldsJson,
    string    StockLocationsJson,
    DateTimeOffset UpdatedAt);
