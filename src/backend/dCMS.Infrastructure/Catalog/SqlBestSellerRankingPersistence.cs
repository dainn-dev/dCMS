using System.Text;
using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

public sealed class SqlBestSellerRankingPersistence(string connectionString) : IBestSellerRankingPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<BestSellerProductRow>> RankAsync(BestSellerRankingCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria.MaxItems <= 0)
            return Array.Empty<BestSellerProductRow>();

        if (string.Equals(criteria.RecommendationLogic, "manual", StringComparison.OrdinalIgnoreCase))
            return await RankManualAsync(criteria, cancellationToken).ConfigureAwait(false);

        var orderBy = criteria.RecommendationLogic.ToLowerInvariant() switch
        {
            "sales-amount" =>
                @"COALESCE(p.""SalesCount30d"", 0) * COALESCE(min_price.""MinPrice"", 0) DESC, p.""UpdatedAt"" DESC",
            "views" => @"p.""PageViews30d"" DESC, p.""UpdatedAt"" DESC",
            _ => @"p.""SalesCount30d"" DESC, p.""UpdatedAt"" DESC"
        };

        var sql = BuildBaseSelect();
        var parameters = BuildBaseParameters(criteria);
        AppendFilters(sql, parameters, criteria);
        sql.Append(" ORDER BY ").Append(orderBy);
        sql.Append(" LIMIT @Limit");

        return await ExecuteAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<BestSellerProductRow>> RankManualAsync(
        BestSellerRankingCriteria criteria, CancellationToken cancellationToken)
    {
        var ids = criteria.ManualProductIds.Take(criteria.MaxItems).ToArray();
        if (ids.Length == 0)
            return Array.Empty<BestSellerProductRow>();

        var sql = BuildBaseSelect();
        var parameters = BuildBaseParameters(criteria);
        parameters.Add("ManualIds", ids);
        sql.Append(" AND p.\"Id\" = ANY(@ManualIds)");
        AppendFilters(sql, parameters, criteria, skipIncludedFilter: true);
        sql.Append(" ORDER BY array_position(@ManualIds, p.\"Id\")");

        return await ExecuteAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private static StringBuilder BuildBaseSelect() => new("""
        SELECT
            p."Id",
            p."Name"::text AS NameJson,
            p."Slug",
            p."CategoryId",
            p."BrandId",
            p."SalesCount30d",
            p."PageViews30d",
            COALESCE(min_price."MinPrice", 0) AS MinBasePriceAmount
        FROM "Products" p
        LEFT JOIN LATERAL (
            SELECT MIN(v."BasePriceAmount") AS "MinPrice"
            FROM "ProductVariants" v
            WHERE v."ProductId" = p."Id" AND v."Status" = 'active'
        ) min_price ON TRUE
        WHERE p."TenantId" = @TenantId
          AND p."StoreId" = @StoreId
          AND p."Status" = 'active'
          AND (p."PublishFrom" IS NULL OR p."PublishFrom" <= @Now)
          AND (p."PublishUntil" IS NULL OR p."PublishUntil" >= @Now)
        """);

    private static DynamicParameters BuildBaseParameters(BestSellerRankingCriteria criteria)
    {
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", criteria.TenantId);
        parameters.Add("StoreId", criteria.StoreId);
        parameters.Add("Now", DateTimeOffset.UtcNow);
        parameters.Add("Limit", criteria.MaxItems);
        return parameters;
    }

    private async Task<IReadOnlyList<BestSellerProductRow>> ExecuteAsync(
        StringBuilder sql, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var rows = await conn.QueryAsync<BestSellerProductRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    private static void AppendFilters(StringBuilder sql, DynamicParameters parameters,
        BestSellerRankingCriteria criteria, bool skipIncludedFilter = false)
    {
        if (criteria.WhitelistedCategoryIds.Length > 0)
        {
            sql.Append(" AND p.\"CategoryId\" = ANY(@WhitelistCats)");
            parameters.Add("WhitelistCats", criteria.WhitelistedCategoryIds);
        }

        if (criteria.BlacklistedCategoryIds.Length > 0)
        {
            sql.Append(" AND NOT (p.\"CategoryId\" = ANY(@BlacklistCats))");
            parameters.Add("BlacklistCats", criteria.BlacklistedCategoryIds);
        }

        if (criteria.WhitelistedBrandIds.Length > 0)
        {
            sql.Append(" AND p.\"BrandId\" = ANY(@WhitelistBrands)");
            parameters.Add("WhitelistBrands", criteria.WhitelistedBrandIds);
        }

        if (criteria.BlacklistedBrandIds.Length > 0)
        {
            sql.Append(" AND (p.\"BrandId\" IS NULL OR NOT (p.\"BrandId\" = ANY(@BlacklistBrands)))");
            parameters.Add("BlacklistBrands", criteria.BlacklistedBrandIds);
        }

        if (!skipIncludedFilter && criteria.IncludedProductIds.Length > 0)
        {
            sql.Append(" AND p.\"Id\" = ANY(@IncludedIds)");
            parameters.Add("IncludedIds", criteria.IncludedProductIds);
        }

        if (criteria.ExcludedProductIds.Length > 0)
        {
            sql.Append(" AND NOT (p.\"Id\" = ANY(@ExcludedIds))");
            parameters.Add("ExcludedIds", criteria.ExcludedProductIds);
        }
    }
}
