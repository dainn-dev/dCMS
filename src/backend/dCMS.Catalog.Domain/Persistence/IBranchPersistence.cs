using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>
/// DAI-750 / US-3: Branch read access (list + nearest by lat/lng).
/// All queries scope by ClientId and active=true unless noted; multi-tenant filtering by
/// TenantId is not applied here — the public storefront endpoint exposes branches across
/// every tenant under a single client (geolocation pre-login).
/// </summary>
public interface IBranchPersistence
{
    /// <summary>Active branches for a client, ordered by Name.</summary>
    Task<IReadOnlyList<Branch>> ListActiveAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nearest active branch within <paramref name="maxKm"/> using Haversine on (Lat,Lng).
    /// Returns the matched branch + distance in km, or <c>null</c> when no branch is within range.
    /// </summary>
    Task<(Branch Branch, double DistanceKm)?> FindNearestAsync(
        string clientId,
        double lat,
        double lng,
        double maxKm,
        CancellationToken cancellationToken = default);

    /// <summary>The single default branch for a client (fallback when nearest is out of range).</summary>
    Task<Branch?> GetDefaultAsync(
        string clientId,
        CancellationToken cancellationToken = default);
}
