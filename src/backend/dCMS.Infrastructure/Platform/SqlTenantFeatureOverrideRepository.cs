using Dapper;
using dCMS.Provisioning.Domain;
using Npgsql;

namespace dCMS.Infrastructure.Platform;

public sealed class SqlTenantFeatureOverrideRepository(string connectionString) : ITenantFeatureOverrideRepository
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<TenantFeatureOverrideRecord>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            """SELECT "TenantId", "Feature", "Enabled", "UpdatedAt" FROM "TenantFeatureOverrides" WHERE "TenantId" = @TenantId""",
            new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new TenantFeatureOverrideRecord(
            r.TenantId,
            r.Feature,
            r.Enabled,
            new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero))).ToList();
    }

    public async Task UpsertAsync(string tenantId, string feature, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "TenantFeatureOverrides" ("TenantId", "Feature", "Enabled", "UpdatedAt")
            VALUES (@TenantId, @Feature, @Enabled, NOW())
            ON CONFLICT ("TenantId", "Feature") DO UPDATE SET "Enabled" = EXCLUDED."Enabled", "UpdatedAt" = NOW()
            """, new { TenantId = tenantId, Feature = feature, Enabled = enabled },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string tenantId, string feature, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "TenantFeatureOverrides" WHERE "TenantId" = @TenantId AND "Feature" = @Feature""",
            new { TenantId = tenantId, Feature = feature }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public string TenantId { get; init; } = "";
        public string Feature { get; init; } = "";
        public bool Enabled { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
