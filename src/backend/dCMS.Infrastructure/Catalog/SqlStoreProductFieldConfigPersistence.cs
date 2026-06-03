using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

public sealed class SqlStoreProductFieldConfigPersistence(string connectionString) : IStoreProductFieldConfigPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<string?> GetFieldsJsonAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Fields"::text
            FROM "StoreProductFieldConfig"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        return await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<(string? FieldsJson, DateTimeOffset? UpdatedAt)> GetFieldsWithUpdatedAtAsync(string tenantId,
        string storeId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Fields"::text AS Fields, "UpdatedAt"
            FROM "StoreProductFieldConfig"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var row = await conn.QuerySingleOrDefaultAsync<(string? Fields, DateTimeOffset? UpdatedAt)>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return (row.Fields, row.UpdatedAt);
    }

    public async Task UpsertFieldsJsonAsync(string tenantId, string storeId, string fieldsJson, DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "StoreProductFieldConfig" ("TenantId", "StoreId", "Fields", "UpdatedAt")
            VALUES (@TenantId, @StoreId, @Fields::jsonb, @UpdatedAt)
            ON CONFLICT ("TenantId", "StoreId") DO UPDATE SET
                "Fields"    = EXCLUDED."Fields",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, StoreId = storeId, Fields = fieldsJson, UpdatedAt = updatedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
