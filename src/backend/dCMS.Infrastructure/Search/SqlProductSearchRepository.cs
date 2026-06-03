using System.Globalization;
using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using Npgsql;

namespace dCMS.Infrastructure.Search;

public sealed class SqlProductSearchRepository(
    ICatalogPersistence catalog,
    IStoreProductFieldConfigPersistence fieldConfig,
    string catalogConnectionString,
    string? inventoryConnectionString,
    CatalogSearchIndexingOptions indexingOptions) : IProductSearchRepository
{
    private readonly string _catalogConnectionString =
        catalogConnectionString ?? throw new ArgumentNullException(nameof(catalogConnectionString));

    private readonly string? _inventoryConnectionString = inventoryConnectionString;
    private readonly CatalogSearchIndexingOptions _indexingOptions = indexingOptions ?? new CatalogSearchIndexingOptions();

    public async Task<ProductIndexPayload?> LoadForIndexAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        var product = await catalog.GetByIdAsync(productId, tenantId, cancellationToken).ConfigureAwait(false);
        if (product is null || !string.Equals(product.StoreId, storeId, StringComparison.Ordinal))
            return null;

        var variants = await catalog
            .ListVariantsForProductAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);

        var (ancestors, categoryPath) = await LoadCategoryIndexMetadataAsync(tenantId, product.CategoryId, cancellationToken)
            .ConfigureAwait(false);

        var stock = await LoadStockSummariesAsync(tenantId, storeId, variants, cancellationToken).ConfigureAwait(false);

        var configJson = await fieldConfig
            .GetFieldsJsonAsync(tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        var fieldDefs = ProductCustomFieldsMapper.ParseDefinitions(configJson);
        var customAttrs = ProductCustomFieldsMapper.ToIndexAttributes(fieldDefs, product.CustomFieldsJson);
        IReadOnlyDictionary<string, string> attributes = new Dictionary<string, string>(customAttrs, StringComparer.OrdinalIgnoreCase);
        const int snapshotVersion = 0;
        var brandId = string.IsNullOrWhiteSpace(product.BrandId) ? null : product.BrandId;

        return new ProductIndexPayload(
            product,
            variants,
            ancestors,
            categoryPath,
            stock,
            attributes,
            snapshotVersion,
            NormalizeCurrency(_indexingOptions.DefaultStoreCurrency),
            brandId);
    }

    private async Task<(IReadOnlyList<int> Ancestors, string Path)> LoadCategoryIndexMetadataAsync(string tenantId,
        int categoryId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_catalogConnectionString);
        const string pathSql = """
            SELECT "Path"
            FROM "Categories"
            WHERE "TenantId" = @TenantId AND "Id" = @CategoryId
            """;
        var rawPath = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(pathSql, new { TenantId = tenantId, CategoryId = categoryId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        const string ancestorSql = """
            WITH RECURSIVE up AS (
                SELECT "Id", "ParentId", 0 AS lvl
                FROM "Categories"
                WHERE "TenantId" = @TenantId AND "Id" = @CategoryId
                UNION ALL
                SELECT c."Id", c."ParentId", up.lvl + 1
                FROM "Categories" c
                INNER JOIN up ON c."Id" = up."ParentId" AND c."TenantId" = @TenantId
            )
            SELECT "Id" FROM up ORDER BY lvl DESC
            """;
        var ids = (await connection
                .QueryAsync<int>(new CommandDefinition(ancestorSql, new { TenantId = tenantId, CategoryId = categoryId },
                    cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToList();

        if (ids.Count == 0)
            ids.Add(categoryId);

        var path = BuildCategoryPath(rawPath, ids);
        return (ids, path);
    }

    private static string BuildCategoryPath(string? pathFromDb, IReadOnlyList<int> ancestorsRootToLeaf)
    {
        var p = pathFromDb?.Trim() ?? string.Empty;
        if (p.Length > 1 && p != "/")
            return p.EndsWith("/", StringComparison.Ordinal) ? p : p + "/";

        if (ancestorsRootToLeaf.Count == 0)
            return "/";

        return "/" + string.Join("/",
                   ancestorsRootToLeaf.Select(i => i.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private async Task<IReadOnlyDictionary<string, VariantStockSummary>> LoadStockSummariesAsync(string tenantId,
        string storeId, IReadOnlyList<ProductVariant> variants, CancellationToken cancellationToken)
    {
        if (variants.Count == 0 || string.IsNullOrWhiteSpace(_inventoryConnectionString))
            return new Dictionary<string, VariantStockSummary>(StringComparer.Ordinal);

        var ids = variants.Select(static v => v.Id).Distinct(StringComparer.Ordinal).ToArray();
        await using var connection = new NpgsqlConnection(_inventoryConnectionString);
        const string sql = """
            SELECT vs."VariantId",
                   SUM(vs."Quantity" - vs."ReservedQuantity")::bigint AS "Available"
            FROM "VariantStock" vs
            INNER JOIN "Warehouses" w ON w."Id" = vs."WarehouseId"
            WHERE w."TenantId" = @TenantId
              AND w."StoreId" = @StoreId
              AND vs."VariantId" = ANY(@VariantIds)
            GROUP BY vs."VariantId"
            """;
        var rows = await connection.QueryAsync<(string VariantId, long Available)>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId, VariantIds = ids },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        var map = new Dictionary<string, VariantStockSummary>(StringComparer.Ordinal);
        foreach (var (variantId, available) in rows)
        {
            var qty = (int)Math.Clamp(available, 0, int.MaxValue);
            map[variantId] = new VariantStockSummary(qty, qty > 0);
        }

        foreach (var v in variants)
        {
            if (!map.ContainsKey(v.Id))
                map[v.Id] = new VariantStockSummary(0, false);
        }

        return map;
    }

    private static string NormalizeCurrency(string? value)
    {
        var c = (value ?? string.Empty).Trim();
        return string.IsNullOrEmpty(c) ? "VND" : c.ToUpperInvariant();
    }
}
