namespace dCMS.Order.Core.Domain;

/// <summary>Snapshot-friendly shipping destination (value object).</summary>
public sealed record ShippingAddress(
    string Line1,
    string? Line2,
    string City,
    string Region,
    string PostalCode,
    string CountryCode);
