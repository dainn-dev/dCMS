namespace dCMS.Core.Models;

/// <summary>
/// DAI-750 / US-3: Physical retail/pickup location under a Client.
/// One branch maps 1:1 to a TenantId scope; the storefront pins to that tenant
/// once geolocation resolves the nearest branch.
/// </summary>
public sealed record Branch(
    Guid           Id,
    string         ClientId,
    string         TenantId,
    string         Name,
    string         Address,
    double         Lat,
    double         Lng,
    bool           IsDefault,
    bool           IsActive,
    DateTimeOffset CreatedAt);
