using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>
/// Dapper + PostgreSQL implementation of <see cref="IBrandFieldConfigPersistence"/>.
/// All queries are scoped by TenantId for multi-tenant isolation.
/// </summary>
public sealed class SqlBrandFieldConfigPersistence(string connectionString) : IBrandFieldConfigPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<string?> GetFieldsJsonAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Fields"::text
            FROM "BrandFieldConfig"
            WHERE "TenantId" = @TenantId
            """;
        return await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpsertFieldsJsonAsync(string tenantId, string fieldsJson, DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "BrandFieldConfig" ("TenantId", "Fields", "UpdatedAt")
            VALUES (@TenantId, @Fields::jsonb, @UpdatedAt)
            ON CONFLICT ("TenantId") DO UPDATE SET
                "Fields"    = EXCLUDED."Fields",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, Fields = fieldsJson, UpdatedAt = updatedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
