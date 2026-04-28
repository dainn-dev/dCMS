using System.Security.Cryptography;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Catalog.Worker.Imports.Processors;

// DAI-708 — product images bulk import row processor.
// Required: sku, image_url. Optional: alt_text, sort_order.
// Resolves productId via ProductVariants.SKU. Downloads image (10s timeout, 10MB cap),
// computes SHA-256, idempotent insert via UQ_ProductImages_Product_Checksum.
public sealed class ProductImageRowProcessor : IImportRowProcessor
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly string _connectionString;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ProductImageRowProcessor> _log;

    public ProductImageRowProcessor(
        string catalogConnectionString,
        IHttpClientFactory http,
        ILogger<ProductImageRowProcessor> log)
    {
        _connectionString = catalogConnectionString;
        _http = http;
        _log = log;
    }

    public string Type => "product-images";

    public async Task<RowResult> ProcessAsync(ImportRow row, ImportContext ctx, CancellationToken ct)
    {
        if (!row.Cells.TryGetValue("sku", out var sku) || string.IsNullOrWhiteSpace(sku))
            return RowResult.Err("sku is required");
        if (!row.Cells.TryGetValue("image_url", out var url) || string.IsNullOrWhiteSpace(url))
            return RowResult.Err("image_url is required");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var imageUri) ||
            (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
            return RowResult.Err("image_url must be http(s) URL");

        var sortOrderRaw = row.Cells.GetValueOrDefault("sort_order");
        int sortOrder = int.TryParse(sortOrderRaw, out var so) ? so : 0;

        await using var cn = new NpgsqlConnection(_connectionString);
        await cn.OpenAsync(ct).ConfigureAwait(false);

        var productId = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT p.""Id""
              FROM ""Products"" p
              JOIN ""ProductVariants"" v ON v.""ProductId"" = p.""Id""
             WHERE p.""TenantId"" = @t AND v.""SKU"" = @sku
             LIMIT 1",
            new { t = ctx.TenantId, sku }, cancellationToken: ct)).ConfigureAwait(false);
        if (productId is null)
            return RowResult.Err($"product not found for sku {sku}");

        byte[] bytes;
        try
        {
            using var client = _http.CreateClient();
            client.Timeout = HttpTimeout;
            using var resp = await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength is long cl && cl > MaxImageBytes)
                return RowResult.Err($"image exceeds {MaxImageBytes} bytes");
            bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (bytes.Length > MaxImageBytes)
                return RowResult.Err($"image exceeds {MaxImageBytes} bytes");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to download image {Url}", url);
            return RowResult.Err($"download failed: {ex.Message}");
        }

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var existing = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT \"Id\" FROM \"ProductImages\" WHERE \"ProductId\" = @p AND \"ChecksumSha256\" = @sha LIMIT 1",
            new { p = productId, sha }, cancellationToken: ct)).ConfigureAwait(false);
        if (existing is not null)
            return RowResult.Ok;

        var imageId = "img_" + NewId.NextSequentialGuid().ToString("N")[..20];
        var storageKey = $"products/{ctx.TenantId}/{productId}/{imageId}.bin";

        await cn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO ""ProductImages""
                (""Id"", ""ProductId"", ""StorageKey"", ""ChecksumSha256"", ""SortOrder"",
                 ""IsPrimary"", ""ImageType"", ""UploadStatus"", ""ContentLength"", ""CreatedAt"")
            VALUES
                (@id, @pid, @key, @sha, @sort, FALSE, 'gallery', 'pending', @len, @now)
            ON CONFLICT (""ProductId"", ""ChecksumSha256"") DO NOTHING",
            new { id = imageId, pid = productId, key = storageKey, sha, sort = sortOrder, len = (long)bytes.Length, now = DateTimeOffset.UtcNow },
            cancellationToken: ct)).ConfigureAwait(false);

        return RowResult.Ok;
    }
}
