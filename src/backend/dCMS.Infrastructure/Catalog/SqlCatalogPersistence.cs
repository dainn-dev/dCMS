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
            SELECT v."Id", v."ProductId", v."SKU", v."CombinationHash", v."CombinationCanonical", v."Status", v."SortOrder"
            FROM "ProductVariants" v
            INNER JOIN "Products" p ON p."Id" = v."ProductId"
            WHERE v."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            ORDER BY v."SortOrder", v."Id"
            """;
        var rows = await connection.QueryAsync<VariantRow>(
            new CommandDefinition(sql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => ProductVariant.Restore(r.Id, r.ProductId, r.Sku, r.CombinationHash, r.Status, r.SortOrder,
                r.CombinationCanonical ?? ""))
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
        string storeId, string sku, string status, int sortOrder, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "ProductVariants" v SET
                "SKU" = @Sku,
                "Status" = @Status,
                "SortOrder" = @SortOrder
            FROM "Products" p
            WHERE v."ProductId" = p."Id"
              AND v."Id" = @VariantId
              AND v."ProductId" = @ProductId
              AND p."TenantId" = @TenantId
              AND p."StoreId" = @StoreId
            """;
        return await connection.ExecuteAsync(new CommandDefinition(sql,
                new { VariantId = variantId, ProductId = productId, TenantId = tenantId, StoreId = storeId, Sku = sku,
                    Status = status, SortOrder = sortOrder }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task BulkInsertVariantsAsync(NpgsqlConnection connection, NpgsqlTransaction tx,
        IReadOnlyList<ProductVariant> variants, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder(
            """INSERT INTO "ProductVariants" ("Id", "ProductId", "SKU", "CombinationHash", "CombinationCanonical", "Status", "SortOrder") VALUES """);
        var ps = new DynamicParameters();
        for (var i = 0; i < variants.Count; i++)
        {
            if (i > 0)
                sql.Append(',');
            var v = variants[i];
            sql.Append($"(@v{i}Id, @v{i}Pid, @v{i}Sku, @v{i}Hash, @v{i}Canon, @v{i}St, @v{i}So)");
            ps.Add($"v{i}Id", v.Id);
            ps.Add($"v{i}Pid", v.ProductId);
            ps.Add($"v{i}Sku", v.Sku);
            ps.Add($"v{i}Hash", v.CombinationHash);
            ps.Add($"v{i}Canon", v.CombinationCanonical);
            ps.Add($"v{i}St", v.Status);
            ps.Add($"v{i}So", v.SortOrder);
        }

        await connection.ExecuteAsync(new CommandDefinition(sql.ToString(), ps, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogCategoryRow>> ListCategoriesByTenantAsync(string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "Id", "ParentId", "Name", "Slug", "Path", "Depth", "SortOrder"
            FROM "Categories"
            WHERE "TenantId" = @TenantId
            ORDER BY "Path", "SortOrder", "Id"
            """;
        var rows = await connection.QueryAsync<CategoryRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(r => new CatalogCategoryRow(r.Id, r.ParentId, r.Name, r.Slug, r.Path, r.Depth, r.SortOrder))
            .ToList();
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
    }

    private sealed class CategoryRow
    {
        public int Id { get; init; }
        public int? ParentId { get; init; }
        public string Name { get; init; } = null!;
        public string Slug { get; init; } = null!;
        public string Path { get; init; } = null!;
        public int Depth { get; init; }
        public int SortOrder { get; init; }
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
}
