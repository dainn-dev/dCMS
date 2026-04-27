namespace dCMS.Core.Models;

/// <summary>Store collection point for fulfillment.</summary>
public sealed record CollectionLocationRow(
    string    Id,
    string    TenantId,
    string    Name,
    string    BrandCodesJson,
    string?   Address1,
    string?   Address2,
    string?   Address3,
    string?   PostalCode,
    string?   Country,
    string?   GeoLat,
    string?   GeoLng,
    string?   DesktopImageSrc,
    string?   DesktopImageName,
    string?   MobileImageSrc,
    string?   MobileImageName,
    bool      Active,
    string?   OpeningHours,
    string?   ClosingHours,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
