using System.Text;
using Dapper;
using dCMS.Core.Events;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

public sealed class SqlCatalogPersistence(string connectionString) : ICatalogPersistence
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<Product?> GetByIdAsync(string productId, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "Id", "TenantId", "StoreId", "CategoryId", "Name", "Description", "Slug", "Status", "SalesCount30d", "CreatedAt", "UpdatedAt"
            FROM "Products"
            WHERE "Id" = @Id AND "TenantId" = @TenantId
            """;
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(
            new CommandDefinition(sql, new { Id = productId, TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null
            ? null
            : Product.Restore(
                row.Id,
                row.TenantId,
                row.StoreId,
                row.CategoryId,
                row.Name,
                row.Description,
                row.Slug,
                ProductStatusExtensions.ParsePersisted(row.Status),
                row.SalesCount30d,
                row.CreatedAt,
                row.UpdatedAt);
    }

    public async Task<Product?> GetBySlugAsync(string storeId, string tenantId, string slug,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "Id", "TenantId", "StoreId", "CategoryId", "Name", "Description", "Slug", "Status", "SalesCount30d", "CreatedAt", "UpdatedAt"
            FROM "Products"
            WHERE "StoreId" = @StoreId AND "TenantId" = @TenantId AND "Slug" = @Slug
            LIMIT 1
            """;
        var normalized = slug.Trim().ToLowerInvariant();
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(
            new CommandDefinition(sql, new { StoreId = storeId, TenantId = tenantId, Slug = normalized },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null
            ? null
            : Product.Restore(
                row.Id,
                row.TenantId,
                row.StoreId,
                row.CategoryId,
                row.Name,
                row.Description,
                row.Slug,
                ProductStatusExtensions.ParsePersisted(row.Status),
                row.SalesCount30d,
                row.CreatedAt,
                row.UpdatedAt);
    }

    public async Task<bool> SlugExistsAsync(string storeId, string slug, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM "Products" WHERE "StoreId" = @StoreId AND "Slug" = @Slug
            ) THEN 1 ELSE 0 END
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { StoreId = storeId, Slug = slug }, cancellationToken: cancellationToken))
                   .ConfigureAwait(false) == 1;
    }

    public async Task<bool> SlugExistsForAnotherProductAsync(string storeId, string slug, string excludeProductId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM "Products"
                WHERE "StoreId" = @StoreId AND "Slug" = @Slug AND "Id" <> @ExcludeProductId
            ) THEN 1 ELSE 0 END
            """;
        return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new { StoreId = storeId, Slug = slug, ExcludeProductId = excludeProductId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false) == 1;
    }

    public async Task SaveProductWithOutboxAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var events = product.DomainEvents.ToArray();
        try
        {
            const string existsSql = """SELECT COUNT(1)::int FROM "Products" WHERE "Id" = @Id""";
            var count = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(existsSql, new { product.Id }, tx, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (count == 0)
            {
                const string insert = """
                    INSERT INTO "Products" ("Id", "TenantId", "StoreId", "CategoryId", "Name", "Description", "Slug", "Status", "SalesCount30d", "CreatedAt", "UpdatedAt")
                    VALUES (@Id, @TenantId, @StoreId, @CategoryId, @Name, @Description, @Slug, @Status, @SalesCount30d, @CreatedAt, @UpdatedAt)
                    """;
                await connection.ExecuteAsync(new CommandDefinition(insert, Map(product), tx, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
            else
            {
                const string update = """
                    UPDATE "Products" SET
                        "CategoryId" = @CategoryId,
                        "Name" = @Name,
                        "Description" = @Description,
                        "Slug" = @Slug,
                        "Status" = @Status,
                        "SalesCount30d" = @SalesCount30d,
                        "UpdatedAt" = @UpdatedAt
                    WHERE "Id" = @Id AND "TenantId" = @TenantId
                    """;
                await connection.ExecuteAsync(new CommandDefinition(update, Map(product), tx, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }

            foreach (var ev in events)
            {
                var (eventType, payload) = DomainEventSerializer.ToOutboxRow(ev);
                const string outSql = """
                    INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
                    VALUES (@EventType, @Payload, @CreatedAt, 0)
                    """;
                await connection.ExecuteAsync(new CommandDefinition(outSql,
                        new { EventType = eventType, Payload = payload, CreatedAt = DateTimeOffset.UtcNow }, tx,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<HashSet<string>> GetVariantCombinationHashesAsync(string productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "CombinationHash"
            FROM "ProductVariants"
            WHERE "ProductId" = @ProductId
            """;
        var rows = await connection
            .QueryAsync<string>(new CommandDefinition(sql, new { ProductId = productId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<int> GetMaxVariantSortOrderAsync(string productId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT COALESCE(MAX("SortOrder"), -1)
            FROM "ProductVariants"
            WHERE "ProductId" = @ProductId
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task SaveNewVariantsWithProductAsync(Product product, IReadOnlyList<ProductVariant> newVariants,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var events = product.DomainEvents.ToArray();
        try
        {
            if (newVariants.Count > 0)
                await BulkInsertVariantsAsync(connection, tx, newVariants, cancellationToken).ConfigureAwait(false);

            const string update = """
                UPDATE "Products" SET
                    "CategoryId" = @CategoryId,
                    "Name" = @Name,
                    "Description" = @Description,
                    "Slug" = @Slug,
                    "Status" = @Status,
                    "SalesCount30d" = @SalesCount30d,
                    "UpdatedAt" = @UpdatedAt
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """;
            await connection.ExecuteAsync(new CommandDefinition(update, Map(product), tx, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            foreach (var ev in events)
            {
                var (eventType, payload) = DomainEventSerializer.ToOutboxRow(ev);
                const string outSql = """
                    INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
                    VALUES (@EventType, @Payload, @CreatedAt, 0)
                    """;
                await connection.ExecuteAsync(new CommandDefinition(outSql,
                    new { EventType = eventType, Payload = payload, CreatedAt = DateTimeOffset.UtcNow }, tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<ProductVariant>> ListVariantsForProductAsync(string productId, string tenantId,
        string storeId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT v."Id", v."ProductId", v."SKU", v."CombinationHash", v."CombinationCanonical", v."Status", v."SortOrder", v."BasePriceAmount"
            FROM "ProductVariants" v
            INNER JOIN "Products" p ON p."Id" = v."ProductId"
            WHERE v."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            ORDER BY v."SortOrder", v."Id"
            """;
        var rows = await connection.QueryAsync<VariantRow>(
            new CommandDefinition(sql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => ProductVariant.Restore(r.Id, r.ProductId, r.Sku, r.CombinationHash, r.Status, r.SortOrder,
                r.CombinationCanonical ?? "", r.BasePriceAmount))
            .ToList();
    }

    public async Task<string?> GetProductIdByVariantIdAsync(string variantId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT p."Id"
            FROM "Products" p
            INNER JOIN "ProductVariants" v ON v."ProductId" = p."Id"
            WHERE v."Id" = @VariantId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            """;
        return await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { VariantId = variantId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> VariantSkuTakenByAnotherAsync(string storeId, string sku, string excludeVariantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM "ProductVariants" v
                INNER JOIN "Products" p ON p."Id" = v."ProductId"
                WHERE p."StoreId" = @StoreId AND v."SKU" = @Sku AND v."Id" <> @ExcludeVariantId
            ) THEN 1 ELSE 0 END
            """;
        return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new { StoreId = storeId, Sku = sku, ExcludeVariantId = excludeVariantId },
                    cancellationToken: cancellationToken)).ConfigureAwait(false) ==
            1;
    }

    public async Task<int> UpdateProductVariantAsync(string variantId, string productId, string tenantId,
        string storeId, string sku, string status, int sortOrder, long basePriceAmount,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "ProductVariants" v SET
                "SKU" = @Sku,
                "Status" = @Status,
                "SortOrder" = @SortOrder,
                "BasePriceAmount" = @BasePriceAmount
            FROM "Products" p
            WHERE v."ProductId" = p."Id"
              AND v."Id" = @VariantId
              AND v."ProductId" = @ProductId
              AND p."TenantId" = @TenantId
              AND p."StoreId" = @StoreId
            """;
        return await connection.ExecuteAsync(new CommandDefinition(sql,
                new { VariantId = variantId, ProductId = productId, TenantId = tenantId, StoreId = storeId, Sku = sku,
                    Status = status, SortOrder = sortOrder, BasePriceAmount = basePriceAmount },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task BulkInsertVariantsAsync(NpgsqlConnection connection, NpgsqlTransaction tx,
        IReadOnlyList<ProductVariant> variants, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder(
            """INSERT INTO "ProductVariants" ("Id", "ProductId", "SKU", "CombinationHash", "CombinationCanonical", "Status", "SortOrder", "BasePriceAmount") VALUES """);
        var ps = new DynamicParameters();
        for (var i = 0; i < variants.Count; i++)
        {
            if (i > 0)
                sql.Append(',');
            var v = variants[i];
            sql.Append($"(@v{i}Id, @v{i}Pid, @v{i}Sku, @v{i}Hash, @v{i}Canon, @v{i}St, @v{i}So, @v{i}Price)");
            ps.Add($"v{i}Id", v.Id);
            ps.Add($"v{i}Pid", v.ProductId);
            ps.Add($"v{i}Sku", v.Sku);
            ps.Add($"v{i}Hash", v.CombinationHash);
            ps.Add($"v{i}Canon", v.CombinationCanonical);
            ps.Add($"v{i}St", v.Status);
            ps.Add($"v{i}So", v.SortOrder);
            ps.Add($"v{i}Price", v.BasePriceAmount);
        }

        await connection.ExecuteAsync(new CommandDefinition(sql.ToString(), ps, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private const string CategoryColumns = """
        "Id","TenantId","ParentId","Path","Depth","Name","Slug","SortOrder",
        "Active","PublishFrom","PublishUntil",
        "ImageMenuUrl","ImagePageUrl","ImageThumbUrl",
        "ShowInNav","ShowInBrands","CustomNavUrl","NavSortPriority","BreakNavColumn",
        "DefaultSort","NoRecommendations",
        "MetaTitleJson","MetaKeywordsJson","MetaDescJson",
        "RestrictAccess","AccessApp","AccessMemberType","AccessMemberTier"
        """;

    public async Task<IReadOnlyList<CatalogCategoryRow>> ListCategoriesByTenantAsync(string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = $"""
            SELECT {CategoryColumns}
            FROM "Categories"
            WHERE "TenantId" = @TenantId
            ORDER BY "Path", "SortOrder", "Id"
            """;
        var rows = await connection.QueryAsync<CategoryRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<CatalogCategoryRow?> GetCategoryByIdAsync(int id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = $"""
            SELECT {CategoryColumns}
            FROM "Categories"
            WHERE "Id" = @Id AND "TenantId" = @TenantId
            """;
        var row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(
            new CommandDefinition(sql, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<bool> CategorySlugExistsAsync(string tenantId, string slug, int? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = excludeId.HasValue
            ? """SELECT CASE WHEN EXISTS(SELECT 1 FROM "Categories" WHERE "TenantId"=@TenantId AND "Slug"=@Slug AND "Id"<>@ExcludeId) THEN 1 ELSE 0 END"""
            : """SELECT CASE WHEN EXISTS(SELECT 1 FROM "Categories" WHERE "TenantId"=@TenantId AND "Slug"=@Slug) THEN 1 ELSE 0 END""";
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, Slug = slug, ExcludeId = excludeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    public async Task<int> CreateCategoryAsync(CatalogCategoryRow row, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        // Compute path + depth from parent
        string path;
        int depth;
        if (row.ParentId.HasValue)
        {
            var parent = await GetCategoryByIdAsync(row.ParentId.Value, row.TenantId, cancellationToken)
                .ConfigureAwait(false);
            if (parent is null) throw new ArgumentException($"Parent category {row.ParentId} not found.");
            path  = parent.Path.TrimEnd('/') + "/";
            depth = parent.Depth + 1;
        }
        else
        {
            path  = "/";
            depth = 0;
        }

        const string sql = """
            INSERT INTO "Categories"
            ("TenantId","ParentId","Path","Depth","Name","Slug","SortOrder",
             "Active","PublishFrom","PublishUntil",
             "ImageMenuUrl","ImagePageUrl","ImageThumbUrl",
             "ShowInNav","ShowInBrands","CustomNavUrl","NavSortPriority","BreakNavColumn",
             "DefaultSort","NoRecommendations",
             "MetaTitleJson","MetaKeywordsJson","MetaDescJson",
             "RestrictAccess","AccessApp","AccessMemberType","AccessMemberTier")
            VALUES
            (@TenantId,@ParentId,@Path,@Depth,@Name,@Slug,@SortOrder,
             @Active,@PublishFrom,@PublishUntil,
             @ImageMenuUrl,@ImagePageUrl,@ImageThumbUrl,
             @ShowInNav,@ShowInBrands,@CustomNavUrl,@NavSortPriority,@BreakNavColumn,
             @DefaultSort,@NoRecommendations,
             @MetaTitleJson,@MetaKeywordsJson,@MetaDescJson,
             @RestrictAccess,@AccessApp,@AccessMemberType,@AccessMemberTier)
            RETURNING "Id"
            """;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            row.TenantId, row.ParentId, Path = path, Depth = depth,
            row.Name, row.Slug, row.SortOrder,
            row.Active, PublishFrom = row.PublishFrom?.UtcDateTime, PublishUntil = row.PublishUntil?.UtcDateTime,
            row.ImageMenuUrl, row.ImagePageUrl, row.ImageThumbUrl,
            row.ShowInNav, row.ShowInBrands, row.CustomNavUrl, row.NavSortPriority, row.BreakNavColumn,
            row.DefaultSort, row.NoRecommendations,
            row.MetaTitleJson, row.MetaKeywordsJson, row.MetaDescJson,
            row.RestrictAccess, row.AccessApp, row.AccessMemberType, row.AccessMemberTier,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateCategoryAsync(CatalogCategoryRow row, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "Categories" SET
                "Name"             = @Name,
                "Slug"             = @Slug,
                "SortOrder"        = @SortOrder,
                "Active"           = @Active,
                "PublishFrom"      = @PublishFrom,
                "PublishUntil"     = @PublishUntil,
                "ImageMenuUrl"     = @ImageMenuUrl,
                "ImagePageUrl"     = @ImagePageUrl,
                "ImageThumbUrl"    = @ImageThumbUrl,
                "ShowInNav"        = @ShowInNav,
                "ShowInBrands"     = @ShowInBrands,
                "CustomNavUrl"     = @CustomNavUrl,
                "NavSortPriority"  = @NavSortPriority,
                "BreakNavColumn"   = @BreakNavColumn,
                "DefaultSort"      = @DefaultSort,
                "NoRecommendations"= @NoRecommendations,
                "MetaTitleJson"    = @MetaTitleJson,
                "MetaKeywordsJson" = @MetaKeywordsJson,
                "MetaDescJson"     = @MetaDescJson,
                "RestrictAccess"   = @RestrictAccess,
                "AccessApp"        = @AccessApp,
                "AccessMemberType" = @AccessMemberType,
                "AccessMemberTier" = @AccessMemberTier
            WHERE "Id" = @Id AND "TenantId" = @TenantId
            """;
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.TenantId, row.Name, row.Slug, row.SortOrder,
            row.Active, PublishFrom = row.PublishFrom?.UtcDateTime, PublishUntil = row.PublishUntil?.UtcDateTime,
            row.ImageMenuUrl, row.ImagePageUrl, row.ImageThumbUrl,
            row.ShowInNav, row.ShowInBrands, row.CustomNavUrl, row.NavSortPriority, row.BreakNavColumn,
            row.DefaultSort, row.NoRecommendations,
            row.MetaTitleJson, row.MetaKeywordsJson, row.MetaDescJson,
            row.RestrictAccess, row.AccessApp, row.AccessMemberType, row.AccessMemberTier,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> DeleteCategoryAsync(int id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        // Delete node and all descendants using recursive CTE
        const string sql = """
            WITH RECURSIVE subtree AS (
                SELECT "Id" FROM "Categories" WHERE "Id" = @Id AND "TenantId" = @TenantId
                UNION ALL
                SELECT c."Id" FROM "Categories" c
                INNER JOIN subtree s ON c."ParentId" = s."Id" AND c."TenantId" = @TenantId
            )
            DELETE FROM "Categories" WHERE "Id" IN (SELECT "Id" FROM subtree)
            """;
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> ReclassifyCategoryAsync(int id, string tenantId, int? newParentId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Determine new path/depth from new parent
        string newParentPath = "/";
        int    newParentDepth = -1;
        if (newParentId.HasValue)
        {
            var parent = await GetCategoryByIdAsync(newParentId.Value, tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (parent is null) throw new ArgumentException($"Target parent category {newParentId} not found.");
            newParentPath  = parent.Path.TrimEnd('/') + "/";
            newParentDepth = parent.Depth;
        }

        // Get current node
        var node = await GetCategoryByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (node is null) return false;

        var oldPath  = node.Path;
        var newPath  = newParentPath;
        var depthDiff = (newParentDepth + 1) - node.Depth;

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Update parent + path/depth for node
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "Categories"
            SET "ParentId" = @NewParentId,
                "Path"     = @NewPath,
                "Depth"    = @NewDepth
            WHERE "Id" = @Id AND "TenantId" = @TenantId
            """, new { NewParentId = newParentId, NewPath = newPath, NewDepth = node.Depth + depthDiff, Id = id, TenantId = tenantId },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Recompute Path + Depth for all descendants
        // Descendants have Path starting with oldPath + id + "/"
        var descendantPathPrefix = oldPath.TrimEnd('/') + $"/{id}/"; // not used — use recursive CTE
        await connection.ExecuteAsync(new CommandDefinition("""
            WITH RECURSIVE subtree AS (
                SELECT "Id","Path","Depth" FROM "Categories"
                WHERE "ParentId" = @RootId AND "TenantId" = @TenantId
                UNION ALL
                SELECT c."Id", c."Path", c."Depth" FROM "Categories" c
                INNER JOIN subtree s ON c."ParentId" = s."Id" AND c."TenantId" = @TenantId
            )
            UPDATE "Categories" c SET
                "Depth" = c."Depth" + @DepthDiff,
                "Path"  = @NewPath || c."Id"::text || '/'
            FROM subtree WHERE c."Id" = subtree."Id"
            """, new { RootId = id, TenantId = tenantId, DepthDiff = depthDiff, NewPath = newPath + id + "/" },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ReorderSiblingsAsync(string tenantId, int? parentId,
        IReadOnlyList<(int Id, int SortOrder)> order,
        CancellationToken cancellationToken = default)
    {
        if (order.Count == 0) return;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (catId, sortOrder) in order)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE "Categories" SET "SortOrder" = @SortOrder
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new { Id = catId, TenantId = tenantId, SortOrder = sortOrder },
                tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogVariantAxisDefinition>> ListVariantAxesForStoreAsync(string tenantId,
        string storeId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string countSql = """
            SELECT COUNT(*)::bigint
            FROM "StoreCatalogAttributeValues"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var allowCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, new { TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        const string sqlAll = """
            SELECT a."Id" AS AttrId, a."Name" AS AttrName, a."SortOrder" AS AttrSort,
                   v."Id" AS ValId, v."Name" AS ValName, v."SortOrder" AS ValSort
            FROM "CatalogAttributes" a
            INNER JOIN "CatalogAttributeValues" v ON v."AttributeId" = a."Id"
            WHERE a."TenantId" = @TenantId
            ORDER BY a."SortOrder", a."Id", v."SortOrder", v."Id"
            """;

        const string sqlFiltered = """
            SELECT a."Id" AS AttrId, a."Name" AS AttrName, a."SortOrder" AS AttrSort,
                   v."Id" AS ValId, v."Name" AS ValName, v."SortOrder" AS ValSort
            FROM "CatalogAttributes" a
            INNER JOIN "CatalogAttributeValues" v ON v."AttributeId" = a."Id"
            INNER JOIN "StoreCatalogAttributeValues" s
                ON s."AttributeValueId" = v."Id" AND s."TenantId" = @TenantId AND s."StoreId" = @StoreId
            WHERE a."TenantId" = @TenantId
            ORDER BY a."SortOrder", a."Id", v."SortOrder", v."Id"
            """;

        var sql = allowCount > 0 ? sqlFiltered : sqlAll;
        var flat = (await connection.QueryAsync<AxisFlatRow>(
                new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return flat
            .GroupBy(r => new { r.AttrId, r.AttrName, r.AttrSort })
            .OrderBy(g => g.Key.AttrSort).ThenBy(g => g.Key.AttrId)
            .Select(g => new CatalogVariantAxisDefinition(
                g.Key.AttrId,
                g.Key.AttrName,
                g.Select(r => new CatalogVariantAxisValue(r.ValId, r.ValName, r.ValSort))
                    .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ApprovalCommentRow>> ListApprovalCommentsForProductAsync(string productId, string tenantId,
        string storeId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT c."Id", c."UserId", c."Role", c."Message", c."Type", c."CreatedAt"
            FROM "ApprovalComments" c
            INNER JOIN "Products" p ON p."Id" = c."ProductId"
            WHERE c."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            ORDER BY c."CreatedAt" ASC, c."Id" ASC
            """;
        var rows = await connection
            .QueryAsync<ApprovalCommentRow>(
                new CommandDefinition(sql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task InsertApprovalCommentAsync(string productId, string userId, string role, string message, string type,
        DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "ApprovalComments" ("ProductId", "UserId", "Role", "Message", "Type", "CreatedAt")
            VALUES (@ProductId, @UserId, @Role, @Message, @Type, @CreatedAt)
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { ProductId = productId, UserId = userId, Role = role, Message = message, Type = type, CreatedAt = createdAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> CountUnreadNotificationsAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT COUNT(*)::int
            FROM "NotificationEvents"
            WHERE "TenantId" = @TenantId AND "UserId" = @UserId AND "ReadAt" IS NULL
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationEventRow>> ListNotificationsForUserAsync(string tenantId, string userId, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "Id", "TenantId", "UserId", "Type", "EntityId", "Message", "ReadAt", "CreatedAt"
            FROM "NotificationEvents"
            WHERE "TenantId" = @TenantId AND "UserId" = @UserId
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Limit
            """;
        var rows = await connection
            .QueryAsync<NotificationEventRow>(
                new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, Limit = limit },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<int> MarkAllNotificationsReadAsync(string tenantId, string userId, DateTimeOffset readAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "NotificationEvents"
            SET "ReadAt" = @ReadAt
            WHERE "TenantId" = @TenantId AND "UserId" = @UserId AND "ReadAt" IS NULL
            """;
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ReadAt = readAt },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task InsertNotificationAsync(string tenantId, string userId, string type, string entityId, string message,
        DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "NotificationEvents" ("TenantId", "UserId", "Type", "EntityId", "Message", "ReadAt", "CreatedAt")
            VALUES (@TenantId, @UserId, @Type, @EntityId, @Message, NULL, @CreatedAt)
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, UserId = userId, Type = type, EntityId = entityId, Message = message, CreatedAt = createdAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<StoreCatalogSettingsRow?> GetStoreCatalogSettingsAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "TenantId", "StoreId", "ApprovalRequired", "LowStockThreshold", "UpdatedAt"
            FROM "StoreCatalogSettings"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        return await connection.QuerySingleOrDefaultAsync<StoreCatalogSettingsRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpsertStoreCatalogSettingsAsync(string tenantId, string storeId, bool approvalRequired, int? lowStockThreshold,
        DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "StoreCatalogSettings" ("TenantId", "StoreId", "ApprovalRequired", "LowStockThreshold", "UpdatedAt")
            VALUES (@TenantId, @StoreId, @ApprovalRequired, @LowStockThreshold, @UpdatedAt)
            ON CONFLICT ("TenantId", "StoreId") DO UPDATE SET
                "ApprovalRequired" = EXCLUDED."ApprovalRequired",
                "LowStockThreshold" = EXCLUDED."LowStockThreshold",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                ApprovalRequired = approvalRequired,
                LowStockThreshold = lowStockThreshold,
                UpdatedAt = updatedAt
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string?> GetLatestApprovalCommentUserIdAsync(string productId, string tenantId, string storeId, string type,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT c."UserId"
            FROM "ApprovalComments" c
            INNER JOIN "Products" p ON p."Id" = c."ProductId"
            WHERE c."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId AND c."Type" = @Type
            ORDER BY c."CreatedAt" DESC, c."Id" DESC
            LIMIT 1
            """;
        return await connection.QueryFirstOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId, Type = type },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class AxisFlatRow
    {
        public int AttrId { get; init; }
        public string AttrName { get; init; } = "";
        public int AttrSort { get; init; }
        public int ValId { get; init; }
        public string ValName { get; init; } = "";
        public int ValSort { get; init; }
    }

    private static object Map(Product p) => new
    {
        p.Id,
        p.TenantId,
        p.StoreId,
        p.CategoryId,
        Name = p.NameJson,
        Description = p.DescriptionJson,
        p.Slug,
        Status = p.Status.ToPersistedValue(),
        SalesCount30d = p.SalesCount30d,
        p.CreatedAt,
        p.UpdatedAt
    };

    private sealed class VariantRow
    {
        public string Id { get; init; } = null!;
        public string ProductId { get; init; } = null!;
        public string Sku { get; init; } = null!;
        public string CombinationHash { get; init; } = null!;
        public string? CombinationCanonical { get; init; }
        public string Status { get; init; } = null!;
        public int SortOrder { get; init; }
        public long BasePriceAmount { get; init; }
    }

    private sealed class CategoryRow
    {
        public int      Id               { get; init; }
        public string   TenantId         { get; init; } = null!;
        public int?     ParentId         { get; init; }
        public string   Path             { get; init; } = null!;
        public int      Depth            { get; init; }
        public string   Name             { get; init; } = null!;
        public string   Slug             { get; init; } = null!;
        public int      SortOrder        { get; init; }
        public bool     Active           { get; init; }
        public DateTime? PublishFrom     { get; init; }
        public DateTime? PublishUntil    { get; init; }
        public string   ImageMenuUrl     { get; init; } = "";
        public string   ImagePageUrl     { get; init; } = "";
        public string   ImageThumbUrl    { get; init; } = "";
        public bool     ShowInNav        { get; init; }
        public bool     ShowInBrands     { get; init; }
        public string   CustomNavUrl     { get; init; } = "";
        public int      NavSortPriority  { get; init; }
        public bool     BreakNavColumn   { get; init; }
        public string   DefaultSort      { get; init; } = "bestseller";
        public bool     NoRecommendations{ get; init; }
        public string   MetaTitleJson    { get; init; } = "{}";
        public string   MetaKeywordsJson { get; init; } = "{}";
        public string   MetaDescJson     { get; init; } = "{}";
        public bool     RestrictAccess   { get; init; }
        public string   AccessApp        { get; init; } = "";
        public string   AccessMemberType { get; init; } = "";
        public string   AccessMemberTier { get; init; } = "";

        public CatalogCategoryRow ToModel() => new(
            Id, TenantId, ParentId, Path, Depth, Name, Slug, SortOrder,
            Active,
            PublishFrom.HasValue  ? new DateTimeOffset(PublishFrom.Value,  TimeSpan.Zero) : null,
            PublishUntil.HasValue ? new DateTimeOffset(PublishUntil.Value, TimeSpan.Zero) : null,
            ImageMenuUrl, ImagePageUrl, ImageThumbUrl,
            ShowInNav, ShowInBrands, CustomNavUrl, NavSortPriority, BreakNavColumn,
            DefaultSort, NoRecommendations,
            MetaTitleJson, MetaKeywordsJson, MetaDescJson,
            RestrictAccess, AccessApp, AccessMemberType, AccessMemberTier);
    }

    private sealed class ProductRow
    {
        public string Id { get; init; } = null!;
        public string TenantId { get; init; } = null!;
        public string StoreId { get; init; } = null!;
        public int CategoryId { get; init; }
        public string Name { get; init; } = null!;
        public string Description { get; init; } = null!;
        public string Slug { get; init; } = null!;
        public string Status { get; init; } = null!;
        public int SalesCount30d { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    // ── Attribute management (DAI-592) ────────────────────────────────────────

    private const string AttrCols = """
        "Id","TenantId","Name","Code","Type","Required","Description","SortOrder","CreatedAt","UpdatedAt"
        """;

    private sealed class AttrRow
    {
        public int      Id          { get; init; }
        public string   TenantId    { get; init; } = null!;
        public string   Name        { get; init; } = null!;
        public string   Code        { get; init; } = "";
        public string   Type        { get; init; } = "TEXT";
        public bool     Required    { get; init; }
        public string   Description { get; init; } = "";
        public int      SortOrder   { get; init; }
        public DateTime CreatedAt   { get; init; }
        public DateTime UpdatedAt   { get; init; }

        public CatalogAttributeRow ToModel() => new(Id, TenantId, Name, Code, Type, Required, Description, SortOrder,
            new DateTimeOffset(CreatedAt, TimeSpan.Zero), new DateTimeOffset(UpdatedAt, TimeSpan.Zero));
    }

    private sealed class AttrValueRow
    {
        public int      Id          { get; init; }
        public int      AttributeId { get; init; }
        public string   Name        { get; init; } = null!;
        public string   Code        { get; init; } = "";
        public string   ColorHex    { get; init; } = "";
        public string   ImageUrl    { get; init; } = "";
        public int      SortOrder   { get; init; }
        public DateTime CreatedAt   { get; init; }

        public CatalogAttributeValueRow ToModel() => new(Id, AttributeId, Name, Code, ColorHex, ImageUrl, SortOrder,
            new DateTimeOffset(CreatedAt, TimeSpan.Zero));
    }

    public async Task<IReadOnlyList<CatalogAttributeRow>> ListAttributesAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var sql = $"""
            SELECT {AttrCols} FROM "CatalogAttributes"
            WHERE "TenantId" = @TenantId
            ORDER BY "SortOrder","Name"
            LIMIT @PageSize OFFSET @Offset
            """;
        var rows = await conn.QueryAsync<AttrRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, PageSize = pageSize, Offset = Math.Max(0, page - 1) * pageSize },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<int> CountAttributesAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("""SELECT COUNT(*)::INT FROM "CatalogAttributes" WHERE "TenantId"=@TenantId""",
                new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<CatalogAttributeRow?> GetAttributeByIdAsync(int id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<AttrRow>(
            new CommandDefinition($"""SELECT {AttrCols} FROM "CatalogAttributes" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<bool> AttributeCodeExistsAsync(string tenantId, string code, int? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var sql = excludeId.HasValue
            ? """SELECT CASE WHEN EXISTS(SELECT 1 FROM "CatalogAttributes" WHERE "TenantId"=@TenantId AND "Code"=@Code AND "Id"<>@Excl) THEN 1 ELSE 0 END"""
            : """SELECT CASE WHEN EXISTS(SELECT 1 FROM "CatalogAttributes" WHERE "TenantId"=@TenantId AND "Code"=@Code) THEN 1 ELSE 0 END""";
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code, Excl = excludeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    public async Task<int> CreateAttributeAsync(CatalogAttributeRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "CatalogAttributes"("TenantId","Name","Code","Type","Required","Description","SortOrder","CreatedAt","UpdatedAt")
            VALUES(@TenantId,@Name,@Code,@Type,@Required,@Description,@SortOrder,@CreatedAt,@UpdatedAt)
            RETURNING "Id"
            """;
        var now = DateTimeOffset.UtcNow;
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            row.TenantId, row.Name, row.Code, row.Type, row.Required, row.Description, row.SortOrder,
            CreatedAt = now.UtcDateTime, UpdatedAt = now.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAttributeAsync(CatalogAttributeRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "CatalogAttributes"
            SET "Name"=@Name,"Code"=@Code,"Type"=@Type,"Required"=@Required,
                "Description"=@Description,"SortOrder"=@SortOrder,"UpdatedAt"=@UpdatedAt
            WHERE "Id"=@Id AND "TenantId"=@TenantId
            """;
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.TenantId, row.Name, row.Code, row.Type, row.Required,
            row.Description, row.SortOrder, UpdatedAt = DateTimeOffset.UtcNow.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> DeleteAttributeAsync(int id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var affected = await conn.ExecuteAsync(
            new CommandDefinition("""DELETE FROM "CatalogAttributes" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<IReadOnlyList<CatalogAttributeValueRow>> ListAttributeValuesAsync(
        int attributeId, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT v."Id",v."AttributeId",v."Name",v."Code",v."ColorHex",v."ImageUrl",v."SortOrder",v."CreatedAt"
            FROM "CatalogAttributeValues" v
            INNER JOIN "CatalogAttributes" a ON a."Id"=v."AttributeId"
            WHERE v."AttributeId"=@AttributeId AND a."TenantId"=@TenantId
            ORDER BY v."SortOrder",v."Name"
            """;
        var rows = await conn.QueryAsync<AttrValueRow>(
            new CommandDefinition(sql, new { AttributeId = attributeId, TenantId = tenantId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<int> CreateAttributeValueAsync(CatalogAttributeValueRow row,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "CatalogAttributeValues"("AttributeId","Name","Code","ColorHex","ImageUrl","SortOrder","CreatedAt")
            VALUES(@AttributeId,@Name,@Code,@ColorHex,@ImageUrl,@SortOrder,@CreatedAt)
            RETURNING "Id"
            """;
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            row.AttributeId, row.Name, row.Code, row.ColorHex, row.ImageUrl, row.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAttributeValueAsync(CatalogAttributeValueRow row, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        // Verify ownership via join
        const string sql = """
            UPDATE "CatalogAttributeValues" v
            SET "Name"=@Name,"Code"=@Code,"ColorHex"=@ColorHex,"ImageUrl"=@ImageUrl,"SortOrder"=@SortOrder
            FROM "CatalogAttributes" a
            WHERE v."Id"=@Id AND v."AttributeId"=@AttributeId
              AND a."Id"=v."AttributeId" AND a."TenantId"=@TenantId
            """;
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.AttributeId, row.Name, row.Code, row.ColorHex, row.ImageUrl, row.SortOrder, TenantId = tenantId,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> DeleteAttributeValueAsync(int valueId, int attributeId, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            DELETE FROM "CatalogAttributeValues" v
            USING "CatalogAttributes" a
            WHERE v."Id"=@ValueId AND v."AttributeId"=@AttributeId
              AND a."Id"=v."AttributeId" AND a."TenantId"=@TenantId
            """;
        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { ValueId = valueId, AttributeId = attributeId, TenantId = tenantId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }
}
