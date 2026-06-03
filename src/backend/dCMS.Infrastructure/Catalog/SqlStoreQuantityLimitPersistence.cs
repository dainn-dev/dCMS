using System.Text.Json;
using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

public sealed class SqlStoreQuantityLimitPersistence(string connectionString) : IStoreQuantityLimitPersistence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<StoreQuantityLimitGeneralRow> GetGeneralAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "CartLimitPerProduct", "UpdatedAt"
            FROM "StoreQuantityLimitSettings"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var row = await conn.QuerySingleOrDefaultAsync<(int CartLimitPerProduct, DateTimeOffset UpdatedAt)?>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null
            ? new StoreQuantityLimitGeneralRow(1000, null)
            : new StoreQuantityLimitGeneralRow(row.Value.CartLimitPerProduct, row.Value.UpdatedAt);
    }

    public async Task UpsertGeneralAsync(string tenantId, string storeId, int cartLimitPerProduct,
        DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "StoreQuantityLimitSettings" ("TenantId", "StoreId", "CartLimitPerProduct", "UpdatedAt")
            VALUES (@TenantId, @StoreId, @CartLimitPerProduct, @UpdatedAt)
            ON CONFLICT ("TenantId", "StoreId") DO UPDATE SET
                "CartLimitPerProduct" = EXCLUDED."CartLimitPerProduct",
                "UpdatedAt"           = EXCLUDED."UpdatedAt"
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, StoreId = storeId, CartLimitPerProduct = cartLimitPerProduct, UpdatedAt = updatedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StoreQuantityLimitRuleRow>> ListRulesAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT
                "Id", "TenantId", "StoreId", "Name", "LimitType", "PerProduct", "QuantityLimit",
                "StartDate", "EndDate", "BrandId", "CategoryIds"::text AS CategoryIdsJson,
                "ProductId", "MembershipType", "MembershipTier", "ModifiedBy", "CreatedAt", "UpdatedAt"
            FROM "StoreQuantityLimitRules"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            ORDER BY "StartDate" DESC, "Name" ASC
            """;
        var rows = await conn.QueryAsync<RuleDbRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(MapRule).ToList();
    }

    public async Task<StoreQuantityLimitRuleRow?> GetRuleAsync(string tenantId, string storeId, string ruleId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT
                "Id", "TenantId", "StoreId", "Name", "LimitType", "PerProduct", "QuantityLimit",
                "StartDate", "EndDate", "BrandId", "CategoryIds"::text AS CategoryIdsJson,
                "ProductId", "MembershipType", "MembershipTier", "ModifiedBy", "CreatedAt", "UpdatedAt"
            FROM "StoreQuantityLimitRules"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Id" = @Id
            """;
        var row = await conn.QuerySingleOrDefaultAsync<RuleDbRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId, Id = ruleId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : MapRule(row);
    }

    public async Task InsertRuleAsync(StoreQuantityLimitRuleRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "StoreQuantityLimitRules" (
                "Id", "TenantId", "StoreId", "Name", "LimitType", "PerProduct", "QuantityLimit",
                "StartDate", "EndDate", "BrandId", "CategoryIds", "ProductId",
                "MembershipType", "MembershipTier", "ModifiedBy", "CreatedAt", "UpdatedAt")
            VALUES (
                @Id, @TenantId, @StoreId, @Name, @LimitType, @PerProduct, @QuantityLimit,
                @StartDate, @EndDate, @BrandId, @CategoryIds::jsonb, @ProductId,
                @MembershipType, @MembershipTier, @ModifiedBy, @CreatedAt, @UpdatedAt)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, ToParams(row), cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> UpdateRuleAsync(StoreQuantityLimitRuleRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            UPDATE "StoreQuantityLimitRules" SET
                "Name"           = @Name,
                "LimitType"      = @LimitType,
                "PerProduct"     = @PerProduct,
                "QuantityLimit"  = @QuantityLimit,
                "StartDate"      = @StartDate,
                "EndDate"        = @EndDate,
                "BrandId"        = @BrandId,
                "CategoryIds"    = @CategoryIds::jsonb,
                "ProductId"      = @ProductId,
                "MembershipType" = @MembershipType,
                "MembershipTier" = @MembershipTier,
                "ModifiedBy"     = @ModifiedBy,
                "UpdatedAt"      = @UpdatedAt
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Id" = @Id
            """;
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, ToParams(row), cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> DeleteRuleAsync(string tenantId, string storeId, string ruleId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            DELETE FROM "StoreQuantityLimitRules"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Id" = @Id
            """;
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, StoreId = storeId, Id = ruleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return affected > 0;
    }

    public async Task InsertHistoryAsync(string tenantId, string storeId, string action, string snapshotJson,
        string userId, string userRole, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "StoreQuantityLimitSettingsHistory"
                ("TenantId", "StoreId", "UserId", "UserRole", "Action", "Snapshot", "CreatedAt")
            VALUES (@TenantId, @StoreId, @UserId, @UserRole, @Action, @Snapshot::jsonb, @CreatedAt)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                UserId = userId,
                UserRole = userRole,
                Action = action,
                Snapshot = snapshotJson,
                CreatedAt = createdAt
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QuantityLimitHistoryRow>> ListHistoryAsync(string tenantId, string storeId,
        int limit, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 100);
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Id", "UserId", "UserRole", "Action", "Snapshot"::text AS SnapshotJson, "CreatedAt"
            FROM "StoreQuantityLimitSettingsHistory"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Limit
            """;
        var rows = await conn.QueryAsync<QuantityLimitHistoryRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId, Limit = take },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static object ToParams(StoreQuantityLimitRuleRow row) => new
    {
        row.Id,
        row.TenantId,
        row.StoreId,
        row.Name,
        row.LimitType,
        row.PerProduct,
        row.QuantityLimit,
        StartDate = row.StartDate,
        EndDate = row.EndDate,
        row.BrandId,
        CategoryIds = JsonSerializer.Serialize(row.CategoryIds, Json),
        row.ProductId,
        row.MembershipType,
        row.MembershipTier,
        row.ModifiedBy,
        row.CreatedAt,
        row.UpdatedAt
    };

    private static StoreQuantityLimitRuleRow MapRule(RuleDbRow r) => new(
        r.Id,
        r.TenantId,
        r.StoreId,
        r.Name,
        r.LimitType,
        r.PerProduct,
        r.QuantityLimit,
        DateOnly.FromDateTime(r.StartDate),
        r.EndDate is null ? null : DateOnly.FromDateTime(r.EndDate.Value),
        string.IsNullOrWhiteSpace(r.BrandId) ? null : r.BrandId,
        ParseCategoryIds(r.CategoryIdsJson),
        string.IsNullOrWhiteSpace(r.ProductId) ? null : r.ProductId,
        string.IsNullOrWhiteSpace(r.MembershipType) ? null : r.MembershipType,
        string.IsNullOrWhiteSpace(r.MembershipTier) ? null : r.MembershipTier,
        r.ModifiedBy ?? "",
        r.CreatedAt,
        r.UpdatedAt);

    private static int[] ParseCategoryIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<int[]>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class RuleDbRow
    {
        public string Id { get; init; } = "";
        public string TenantId { get; init; } = "";
        public string StoreId { get; init; } = "";
        public string Name { get; init; } = "";
        public string LimitType { get; init; } = "";
        public bool PerProduct { get; init; }
        public int QuantityLimit { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public string? BrandId { get; init; }
        public string? CategoryIdsJson { get; init; }
        public string? ProductId { get; init; }
        public string? MembershipType { get; init; }
        public string? MembershipTier { get; init; }
        public string? ModifiedBy { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
