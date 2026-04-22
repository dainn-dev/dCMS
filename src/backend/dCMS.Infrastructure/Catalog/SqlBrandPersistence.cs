using System.Text;
using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>
/// Dapper + PostgreSQL implementation of <see cref="IBrandPersistence"/>.
/// All queries are scoped by TenantId for multi-tenant isolation.
/// </summary>
public sealed class SqlBrandPersistence(string connectionString) : IBrandPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    // ── Internal DTO ─────────────────────────────────────────────────────────

    private sealed record BrandRow(
        string   TenantId,
        string   Code,
        string   Name,
        bool     Active,
        string   ImageUrl,
        string   ImageAlt,
        string   AdditionalInfo,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private static Brand ToModel(BrandRow r) =>
        Brand.Restore(r.TenantId, r.Code, r.Name, r.Active, r.ImageUrl, r.ImageAlt,
            r.AdditionalInfo,
            new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero));

    // ── List / Count ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Brand>> ListBrandsAsync(
        string tenantId, bool? activeOnly, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var (where, p) = BuildFilter(tenantId, activeOnly, search);
        p.Add("Offset", Math.Max(0, page - 1) * pageSize);
        p.Add("PageSize", pageSize);

        var sql = $"""
            SELECT "TenantId","Code","Name","Active","ImageUrl","ImageAlt","AdditionalInfo","CreatedAt","UpdatedAt"
            FROM "Brands"
            {where}
            ORDER BY "Name"
            LIMIT @PageSize OFFSET @Offset
            """;

        var rows = await conn.QueryAsync<BrandRow>(
            new CommandDefinition(sql, p, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(ToModel).ToList();
    }

    public async Task<int> CountBrandsAsync(
        string tenantId, bool? activeOnly, string? search,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var (where, p) = BuildFilter(tenantId, activeOnly, search);
        var sql = $"""SELECT COUNT(*)::INT FROM "Brands" {where}""";
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, p, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // ── Single get ────────────────────────────────────────────────────────────

    public async Task<Brand?> GetBrandAsync(
        string tenantId, string code,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "TenantId","Code","Name","Active","ImageUrl","ImageAlt","AdditionalInfo","CreatedAt","UpdatedAt"
            FROM "Brands"
            WHERE "TenantId" = @TenantId AND "Code" = @Code
            """;
        var row = await conn.QuerySingleOrDefaultAsync<BrandRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : ToModel(row);
    }

    // ── Existence check ───────────────────────────────────────────────────────

    public async Task<bool> CodeExistsAsync(
        string tenantId, string code,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM "Brands" WHERE "TenantId" = @TenantId AND "Code" = @Code
            ) THEN 1 ELSE 0 END
            """;
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code },
                cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    // ── Upsert ────────────────────────────────────────────────────────────────

    public async Task SaveBrandAsync(Brand brand, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "Brands" ("TenantId","Code","Name","Active","ImageUrl","ImageAlt","AdditionalInfo","CreatedAt","UpdatedAt")
            VALUES (@TenantId,@Code,@Name,@Active,@ImageUrl,@ImageAlt,@AdditionalInfo::jsonb,@CreatedAt,@UpdatedAt)
            ON CONFLICT ("TenantId","Code") DO UPDATE SET
                "Name"           = EXCLUDED."Name",
                "Active"         = EXCLUDED."Active",
                "ImageUrl"       = EXCLUDED."ImageUrl",
                "ImageAlt"       = EXCLUDED."ImageAlt",
                "AdditionalInfo" = EXCLUDED."AdditionalInfo",
                "UpdatedAt"      = EXCLUDED."UpdatedAt"
            """;

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            brand.TenantId,
            brand.Code,
            brand.Name,
            brand.Active,
            brand.ImageUrl,
            brand.ImageAlt,
            brand.AdditionalInfo,
            brand.CreatedAt,
            brand.UpdatedAt,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteBrandAsync(
        string tenantId, string code,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            DELETE FROM "Brands" WHERE "TenantId" = @TenantId AND "Code" = @Code
            """;
        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return affected > 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string Where, DynamicParameters Params) BuildFilter(
        string tenantId, bool? activeOnly, string? search)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);

        var clauses = new List<string> { "\"TenantId\" = @TenantId" };

        if (activeOnly.HasValue)
        {
            clauses.Add("\"Active\" = @Active");
            p.Add("Active", activeOnly.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            clauses.Add("(\"Name\" ILIKE @Search OR \"Code\" ILIKE @Search)");
            p.Add("Search", $"%{search.Trim()}%");
        }

        var where = "WHERE " + string.Join(" AND ", clauses);
        return (where, p);
    }
}
