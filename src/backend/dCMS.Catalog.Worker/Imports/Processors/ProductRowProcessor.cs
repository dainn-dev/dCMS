using System.Globalization;
using Dapper;
using MassTransit;
using Npgsql;

namespace dCMS.Catalog.Worker.Imports.Processors;

// DAI-708 — products bulk import row processor.
// Required: sku, name, category_id, price.
// Optional: description, store_id, status, slug.
// Idempotent on (TenantId, StoreId, Slug). Note: variant SKU is upserted into ProductVariants.
public sealed class ProductRowProcessor : IImportRowProcessor
{
    private readonly string _connectionString;
    public ProductRowProcessor(string catalogConnectionString) { _connectionString = catalogConnectionString; }

    public string Type => "products";

    public async Task<RowResult> ProcessAsync(ImportRow row, ImportContext ctx, CancellationToken ct)
    {
        if (!row.Cells.TryGetValue("sku", out var sku) || string.IsNullOrWhiteSpace(sku))
            return RowResult.Err("sku is required");
        if (!row.Cells.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return RowResult.Err("name is required");
        if (!row.Cells.TryGetValue("category_id", out var catRaw) || !int.TryParse(catRaw, out var categoryId))
            return RowResult.Err("category_id must be integer");
        if (!row.Cells.TryGetValue("price", out var priceRaw) ||
            !decimal.TryParse(priceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            return RowResult.Err("price must be numeric");

        var description = row.Cells.GetValueOrDefault("description") ?? string.Empty;
        var storeId = row.Cells.GetValueOrDefault("store_id");
        if (string.IsNullOrWhiteSpace(storeId))
            return RowResult.Err("store_id is required");
        var status = row.Cells.GetValueOrDefault("status");
        if (string.IsNullOrWhiteSpace(status)) status = "draft";
        var slug = row.Cells.GetValueOrDefault("slug");
        if (string.IsNullOrWhiteSpace(slug)) slug = Slugify(name);

        await using var cn = new NpgsqlConnection(_connectionString);
        await cn.OpenAsync(ct).ConfigureAwait(false);

        var categoryExists = await cn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT 1 FROM \"Categories\" WHERE \"Id\" = @id LIMIT 1",
                new { id = categoryId }, cancellationToken: ct)).ConfigureAwait(false);
        if (categoryExists == 0)
            return RowResult.Err($"category_id {categoryId} not found");

        var existingProductId = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT \"Id\" FROM \"Products\" WHERE \"TenantId\" = @t AND \"StoreId\" = @s AND \"Slug\" = @slug LIMIT 1",
            new { t = ctx.TenantId, s = storeId, slug }, cancellationToken: ct)).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        string productId;
        if (existingProductId is not null)
        {
            productId = existingProductId;
            await cn.ExecuteAsync(new CommandDefinition(@"
                UPDATE ""Products""
                   SET ""Name"" = @name,
                       ""Description"" = @desc,
                       ""CategoryId"" = @cat,
                       ""Status"" = @status,
                       ""UpdatedAt"" = @now
                 WHERE ""Id"" = @id",
                new { id = productId, name, desc = description, cat = categoryId, status, now },
                cancellationToken: ct)).ConfigureAwait(false);
        }
        else
        {
            productId = "prd_" + NewId.NextSequentialGuid().ToString("N")[..20];
            await cn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO ""Products""
                   (""Id"", ""TenantId"", ""StoreId"", ""CategoryId"", ""Name"", ""Description"", ""Slug"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                   (@id, @t, @s, @cat, @name, @desc, @slug, @status, @now, @now)",
                new { id = productId, t = ctx.TenantId, s = storeId, cat = categoryId, name, desc = description, slug, status, now },
                cancellationToken: ct)).ConfigureAwait(false);
        }

        var variantId = "var_" + ComputeStableId(productId, sku);
        await cn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO ""ProductVariants""
                (""Id"", ""ProductId"", ""SKU"", ""CombinationHash"", ""Status"", ""SortOrder"")
            VALUES (@id, @pid, @sku, @hash, 'active', 0)
            ON CONFLICT (""ProductId"", ""CombinationHash"")
            DO UPDATE SET ""SKU"" = EXCLUDED.""SKU""",
            new { id = variantId, pid = productId, sku, hash = ComputeStableId(productId, sku) },
            cancellationToken: ct)).ConfigureAwait(false);

        return RowResult.Ok;
    }

    private static string Slugify(string name)
    {
        var s = name.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private static string ComputeStableId(string productId, string sku)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(productId + "|" + sku));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }
}
