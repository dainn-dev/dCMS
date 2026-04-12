using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

public sealed class SqlProductImagePersistence(string connectionString) : IProductImagePersistence
{
    private readonly string _connectionString =
        connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<ProductImageRow>> ListForProductAsync(string productId, string tenantId,
        string storeId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT i."Id", i."ProductId", i."StorageKey", i."ChecksumSha256", i."SortOrder", i."IsPrimary", i."ImageType",
                   i."UploadStatus", i."ContentLength", i."CreatedAt"
            FROM "ProductImages" i
            INNER JOIN "Products" p ON p."Id" = i."ProductId"
            WHERE i."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            ORDER BY i."SortOrder", i."Id"
            """;
        var rows = await connection.QueryAsync<ImageRow>(
            new CommandDefinition(sql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<ProductImageRow?> FindByChecksumAsync(string productId, string tenantId, string storeId,
        string checksumSha256, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT i."Id", i."ProductId", i."StorageKey", i."ChecksumSha256", i."SortOrder", i."IsPrimary", i."ImageType",
                   i."UploadStatus", i."ContentLength", i."CreatedAt"
            FROM "ProductImages" i
            INNER JOIN "Products" p ON p."Id" = i."ProductId"
            WHERE i."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
              AND i."ChecksumSha256" = @Checksum
            LIMIT 1
            """;
        var row = await connection.QuerySingleOrDefaultAsync<ImageRow>(
            new CommandDefinition(sql,
                new { ProductId = productId, TenantId = tenantId, StoreId = storeId, Checksum = checksumSha256 },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<ProductImageRow> CreatePendingAsync(string productId, string tenantId, string storeId,
        string checksumSha256, string imageType, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string scopeSql = """
            SELECT p."Id" FROM "Products" p
            WHERE p."Id" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            LIMIT 1
            """;
        var ok = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(scopeSql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (ok is null)
            throw new InvalidOperationException("Product not found for tenant/store.");

        const string nextSortSql = """
            SELECT COALESCE(MAX("SortOrder"), -1) + 1
            FROM "ProductImages"
            WHERE "ProductId" = @ProductId
            """;
        var nextSort = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(nextSortSql, new { ProductId = productId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var id = "img_" + Guid.NewGuid().ToString("N");
        const string insert = """
            INSERT INTO "ProductImages" ("Id", "ProductId", "StorageKey", "ChecksumSha256", "SortOrder", "IsPrimary", "ImageType", "UploadStatus", "ContentLength", "CreatedAt")
            VALUES (@Id, @ProductId, '', @Checksum, @SortOrder, FALSE, @ImageType, 'pending', 0, @CreatedAt)
            """;
        await connection.ExecuteAsync(new CommandDefinition(insert,
                new
                {
                    Id = id,
                    ProductId = productId,
                    Checksum = checksumSha256,
                    SortOrder = nextSort,
                    ImageType = imageType,
                    CreatedAt = now
                }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return new ProductImageRow(id, productId, "", checksumSha256, nextSort, false, imageType, "pending", 0, now);
    }

    public async Task<int> MarkUploadCompleteAsync(string imageId, string productId, string tenantId, string storeId,
        string storageKey, long contentLength, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "ProductImages" i SET
                "StorageKey" = @StorageKey,
                "UploadStatus" = 'ready',
                "ContentLength" = @ContentLength
            FROM "Products" p
            WHERE i."ProductId" = p."Id"
              AND i."Id" = @ImageId
              AND i."ProductId" = @ProductId
              AND p."TenantId" = @TenantId
              AND p."StoreId" = @StoreId
            """;
        return await connection.ExecuteAsync(new CommandDefinition(sql,
                new { ImageId = imageId, ProductId = productId, TenantId = tenantId, StoreId = storeId, StorageKey = storageKey,
                    ContentLength = contentLength },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteForProductAsync(string imageId, string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            DELETE FROM "ProductImages" i
            USING "Products" p
            WHERE i."ProductId" = p."Id"
              AND i."Id" = @ImageId
              AND i."ProductId" = @ProductId
              AND p."TenantId" = @TenantId
              AND p."StoreId" = @StoreId
            """;
        return await connection.ExecuteAsync(new CommandDefinition(sql,
                new { ImageId = imageId, ProductId = productId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task ReorderAsync(string productId, string tenantId, string storeId, IReadOnlyList<string> orderedImageIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string scopeSql = """
            SELECT p."Id" FROM "Products" p
            WHERE p."Id" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            LIMIT 1
            """;
        var scoped = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(scopeSql, new { ProductId = productId, TenantId = tenantId, StoreId = storeId }, tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (scoped is null)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Product not found for tenant/store.");
        }

        const string updateOne = """
            UPDATE "ProductImages" i SET "SortOrder" = @SortOrder
            FROM "Products" p
            WHERE i."ProductId" = p."Id" AND i."Id" = @ImageId AND i."ProductId" = @ProductId
              AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            """;
        for (var i = 0; i < orderedImageIds.Count; i++)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(updateOne,
                    new
                    {
                        SortOrder = i,
                        ImageId = orderedImageIds[i],
                        ProductId = productId,
                        TenantId = tenantId,
                        StoreId = storeId
                    }, tx, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            if (affected != 1)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Reorder failed: unknown image id or wrong product.");
            }
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetPrimaryAsync(string imageId, string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string clear = """
            UPDATE "ProductImages" i SET "IsPrimary" = FALSE
            FROM "Products" p
            WHERE i."ProductId" = p."Id" AND i."ProductId" = @ProductId AND p."TenantId" = @TenantId AND p."StoreId" = @StoreId
            """;
        await connection.ExecuteAsync(new CommandDefinition(clear, new { ProductId = productId, TenantId = tenantId, StoreId = storeId },
                tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        const string setOne = """
            UPDATE "ProductImages" i SET "IsPrimary" = TRUE
            FROM "Products" p
            WHERE i."ProductId" = p."Id"
              AND i."Id" = @ImageId
              AND i."ProductId" = @ProductId
              AND p."TenantId" = @TenantId
              AND p."StoreId" = @StoreId
            """;
        var n = await connection.ExecuteAsync(new CommandDefinition(setOne,
                new { ImageId = imageId, ProductId = productId, TenantId = tenantId, StoreId = storeId }, tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (n != 1)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Primary image update failed.");
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> UpdateImageTypeAsync(string imageId, string productId, string tenantId, string storeId,
        string imageType, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "ProductImages" i SET "ImageType" = @ImageType
            FROM "Products" p
            WHERE i."ProductId" = p."Id"
              AND i."Id" = @ImageId
              AND i."ProductId" = @ProductId
              AND p."TenantId" = @TenantId
              AND p."StoreId" = @StoreId
            """;
        return await connection.ExecuteAsync(new CommandDefinition(sql,
                new { ImageId = imageId, ProductId = productId, TenantId = tenantId, StoreId = storeId, ImageType = imageType },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static ProductImageRow Map(ImageRow r) =>
        new(r.Id, r.ProductId, r.StorageKey, r.ChecksumSha256, r.SortOrder, r.IsPrimary, r.ImageType, r.UploadStatus,
            r.ContentLength, r.CreatedAt);

    private sealed class ImageRow
    {
        public string Id { get; init; } = null!;
        public string ProductId { get; init; } = null!;
        public string StorageKey { get; init; } = "";
        public string ChecksumSha256 { get; init; } = "";
        public int SortOrder { get; init; }
        public bool IsPrimary { get; init; }
        public string ImageType { get; init; } = "";
        public string UploadStatus { get; init; } = "";
        public long ContentLength { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
