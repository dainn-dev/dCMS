using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>
/// DAI-750 / US-3: Dapper + PostgreSQL implementation of <see cref="IBranchPersistence"/>.
/// Haversine is computed in SQL to keep transfer + sort on the database side; Postgres
/// handles &lt;100 branches per client comfortably without PostGIS.
/// </summary>
public sealed class SqlBranchPersistence(string connectionString) : IBranchPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private sealed record BranchRow(
        Guid     Id,
        string   ClientId,
        string   TenantId,
        string   Name,
        string   Address,
        double   Lat,
        double   Lng,
        bool     IsDefault,
        bool     IsActive,
        DateTime CreatedAt);

    private static Branch ToModel(BranchRow r) =>
        new(r.Id, r.ClientId, r.TenantId, r.Name, r.Address, r.Lat, r.Lng,
            r.IsDefault, r.IsActive,
            new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero));

    public async Task<IReadOnlyList<Branch>> ListActiveAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id","ClientId","TenantId","Name","Address","Lat","Lng","IsDefault","IsActive","CreatedAt"
            FROM "Branches"
            WHERE "ClientId" = @ClientId AND "IsActive" = TRUE
            ORDER BY "Name"
            """;

        await using var conn = new NpgsqlConnection(_cs);
        var rows = await conn.QueryAsync<BranchRow>(
            new CommandDefinition(sql, new { ClientId = clientId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(ToModel).ToList();
    }

    public async Task<(Branch Branch, double DistanceKm)?> FindNearestAsync(
        string clientId, double lat, double lng, double maxKm, CancellationToken cancellationToken = default)
    {
        // 6371 km = mean Earth radius. acos-clamp avoids domain errors from floating-point round-up.
        const string sql = """
            SELECT "Id","ClientId","TenantId","Name","Address","Lat","Lng","IsDefault","IsActive","CreatedAt",
                   6371.0 * acos(LEAST(1.0,
                       cos(radians(@Lat)) * cos(radians("Lat")) *
                       cos(radians("Lng") - radians(@Lng)) +
                       sin(radians(@Lat)) * sin(radians("Lat")))) AS "DistanceKm"
            FROM "Branches"
            WHERE "ClientId" = @ClientId AND "IsActive" = TRUE
            ORDER BY "DistanceKm"
            LIMIT 1
            """;

        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QueryFirstOrDefaultAsync<BranchWithDistanceRow>(
            new CommandDefinition(sql, new { ClientId = clientId, Lat = lat, Lng = lng },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null || row.DistanceKm > maxKm)
            return null;

        return (ToModel(new BranchRow(row.Id, row.ClientId, row.TenantId, row.Name, row.Address,
            row.Lat, row.Lng, row.IsDefault, row.IsActive, row.CreatedAt)), row.DistanceKm);
    }

    public async Task<Branch?> GetDefaultAsync(string clientId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id","ClientId","TenantId","Name","Address","Lat","Lng","IsDefault","IsActive","CreatedAt"
            FROM "Branches"
            WHERE "ClientId" = @ClientId AND "IsDefault" = TRUE
            LIMIT 1
            """;

        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QueryFirstOrDefaultAsync<BranchRow>(
            new CommandDefinition(sql, new { ClientId = clientId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row is null ? null : ToModel(row);
    }

    private sealed record BranchWithDistanceRow(
        Guid     Id,
        string   ClientId,
        string   TenantId,
        string   Name,
        string   Address,
        double   Lat,
        double   Lng,
        bool     IsDefault,
        bool     IsActive,
        DateTime CreatedAt,
        double   DistanceKm);
}
