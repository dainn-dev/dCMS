using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

public sealed class SqlStoreBestSellerSettingsPersistence(string connectionString) : IStoreBestSellerSettingsPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<string?> GetSettingsJsonAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        var (json, _) = await GetSettingsWithUpdatedAtAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        return json;
    }

    public async Task<(string? SettingsJson, DateTimeOffset? UpdatedAt)> GetSettingsWithUpdatedAtAsync(
        string tenantId, string storeId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Settings"::text AS SettingsJson, "UpdatedAt"
            FROM "StoreBestSellerSettings"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var row = await conn.QuerySingleOrDefaultAsync<(string? SettingsJson, DateTimeOffset? UpdatedAt)>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row;
    }

    public async Task UpsertSettingsJsonAsync(string tenantId, string storeId, string settingsJson,
        DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "StoreBestSellerSettings" ("TenantId", "StoreId", "Settings", "UpdatedAt")
            VALUES (@TenantId, @StoreId, @Settings::jsonb, @UpdatedAt)
            ON CONFLICT ("TenantId", "StoreId") DO UPDATE SET
                "Settings"  = EXCLUDED."Settings",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, StoreId = storeId, Settings = settingsJson, UpdatedAt = updatedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InsertHistoryAsync(string tenantId, string storeId, string settingsJson, string userId,
        string userRole, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "StoreBestSellerSettingsHistory"
                ("TenantId", "StoreId", "UserId", "UserRole", "Settings", "CreatedAt")
            VALUES (@TenantId, @StoreId, @UserId, @UserRole, @Settings::jsonb, @CreatedAt)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                UserId = userId,
                UserRole = userRole,
                Settings = settingsJson,
                CreatedAt = createdAt
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BestSellerSettingsHistoryRow>> ListHistoryAsync(string tenantId, string storeId,
        int limit, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 100);
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Id", "UserId", "UserRole", "Settings"::text AS SettingsJson, "CreatedAt"
            FROM "StoreBestSellerSettingsHistory"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Limit
            """;
        var rows = await conn.QueryAsync<BestSellerSettingsHistoryRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId, Limit = take },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }
}
