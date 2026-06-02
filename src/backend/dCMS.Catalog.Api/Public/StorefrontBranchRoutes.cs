using dCMS.Catalog.Api.Http;
using dCMS.Core.Models;
using dCMS.Core.Persistence;

namespace dCMS.Catalog.Api.Public;

/// <summary>
/// DAI-750 / US-3: Public storefront branch endpoints.
/// Pre-login geolocation routing — no auth required, IP-keyed rate limit (60 req/min).
/// </summary>
public static class StorefrontBranchRoutes
{
    private const string RateLimitPolicy = "PublicStorefrontBranches";

    public static void MapStorefrontBranchRoutes(this WebApplication app, IConfiguration configuration)
    {
        var clientId = configuration.GetSection("Dcms:Client")["Id"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "Dcms:Client.Id is required for storefront branch endpoints. " +
                "Set \"Dcms\": { \"Client\": { \"Id\": \"<chain>\" } } in appsettings.");

        var g = app.MapGroup("/api/v1/storefront/branches")
            .WithTags("storefront-branches")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        g.MapGet("",         (IBranchPersistence p, CancellationToken ct) => ListBranches(p, clientId, ct));
        g.MapGet("/nearest", (IBranchPersistence p, double? lat, double? lng, double? maxKm, CancellationToken ct) =>
            FindNearest(p, clientId, lat, lng, maxKm, ct));
    }

    private static async Task<IResult> ListBranches(
        IBranchPersistence persistence, string clientId, CancellationToken cancellationToken)
    {
        var rows = await persistence.ListActiveAsync(clientId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(rows.Select(ToDto));
    }

    private static async Task<IResult> FindNearest(
        IBranchPersistence persistence,
        string clientId,
        double? lat,
        double? lng,
        double? maxKm,
        CancellationToken cancellationToken)
    {
        if (lat is null || lng is null)
            return ApiEnvelope.Error("validation_error",
                "Query parameters 'lat' and 'lng' are required.", StatusCodes.Status400BadRequest);

        if (lat is < -90 or > 90)
            return ApiEnvelope.Error("validation_error",
                "lat must be between -90 and 90.", StatusCodes.Status400BadRequest);

        if (lng is < -180 or > 180)
            return ApiEnvelope.Error("validation_error",
                "lng must be between -180 and 180.", StatusCodes.Status400BadRequest);

        var radiusKm = maxKm ?? 10.0;
        if (radiusKm is <= 0 or > 20000)
            return ApiEnvelope.Error("validation_error",
                "maxKm must be a positive number (≤ 20000).", StatusCodes.Status400BadRequest);

        var hit = await persistence.FindNearestAsync(clientId, lat.Value, lng.Value, radiusKm, cancellationToken)
            .ConfigureAwait(false);

        if (hit is { } match)
            return ApiEnvelope.Ok(ToNearestDto(match.Branch, match.DistanceKm, fallback: false));

        var fallback = await persistence.GetDefaultAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (fallback is null)
            return ApiEnvelope.Error("not_found",
                "No active branch within range and no default branch configured.", StatusCodes.Status404NotFound);

        return ApiEnvelope.Ok(ToNearestDto(fallback, distanceKm: null, fallback: true));
    }

    private static object ToDto(Branch b) => new
    {
        id        = b.Id,
        tenantId  = b.TenantId,
        name      = b.Name,
        address   = b.Address,
        lat       = b.Lat,
        lng       = b.Lng,
        isDefault = b.IsDefault,
    };

    private static object ToNearestDto(Branch b, double? distanceKm, bool fallback) => new
    {
        id          = b.Id,
        tenantId    = b.TenantId,
        name        = b.Name,
        address     = b.Address,
        lat         = b.Lat,
        lng         = b.Lng,
        isDefault   = b.IsDefault,
        distanceKm  = distanceKm,
        fallback,
    };
}
