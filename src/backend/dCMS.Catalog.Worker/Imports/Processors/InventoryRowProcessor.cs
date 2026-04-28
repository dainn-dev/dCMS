using Dapper;
using Npgsql;

namespace dCMS.Catalog.Worker.Imports.Processors;

// DAI-708 — inventory bulk import row processor.
// Required: sku, store_id, qty. Updates VariantStock.Quantity for the
// (variant, warehouse) pair belonging to (tenantId, storeId). Increments Revision
// so downstream outbox/index pipelines pick the change up.
public sealed class InventoryRowProcessor : IImportRowProcessor
{
    private readonly string _catalogConnectionString;
    private readonly string _inventoryConnectionString;

    public InventoryRowProcessor(string catalogConnectionString, string inventoryConnectionString)
    {
        _catalogConnectionString = catalogConnectionString;
        _inventoryConnectionString = inventoryConnectionString;
    }

    public string Type => "inventory";

    public async Task<RowResult> ProcessAsync(ImportRow row, ImportContext ctx, CancellationToken ct)
    {
        if (!row.Cells.TryGetValue("sku", out var sku) || string.IsNullOrWhiteSpace(sku))
            return RowResult.Err("sku is required");
        if (!row.Cells.TryGetValue("store_id", out var storeId) || string.IsNullOrWhiteSpace(storeId))
            return RowResult.Err("store_id is required");
        if (!row.Cells.TryGetValue("qty", out var qtyRaw) || !int.TryParse(qtyRaw, out var qty) || qty < 0)
            return RowResult.Err("qty must be non-negative integer");

        await using var cat = new NpgsqlConnection(_catalogConnectionString);
        await cat.OpenAsync(ct).ConfigureAwait(false);
        var variantId = await cat.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT v.""Id""
              FROM ""ProductVariants"" v
              JOIN ""Products"" p ON p.""Id"" = v.""ProductId""
             WHERE p.""TenantId"" = @t AND v.""SKU"" = @sku
             LIMIT 1",
            new { t = ctx.TenantId, sku }, cancellationToken: ct)).ConfigureAwait(false);
        if (variantId is null)
            return RowResult.Err($"variant not found for sku {sku}");

        await using var inv = new NpgsqlConnection(_inventoryConnectionString);
        await inv.OpenAsync(ct).ConfigureAwait(false);

        var warehouseId = await inv.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT \"Id\" FROM \"Warehouses\" WHERE \"TenantId\" = @t AND \"StoreId\" = @s AND \"IsActive\" = TRUE LIMIT 1",
            new { t = ctx.TenantId, s = storeId }, cancellationToken: ct)).ConfigureAwait(false);
        if (warehouseId is null)
            return RowResult.Err($"no active warehouse for store {storeId}");

        var rows = await inv.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO ""VariantStock"" (""VariantId"", ""WarehouseId"", ""Quantity"", ""ReservedQuantity"", ""Revision"")
            VALUES (@v, @w, @q, 0, 1)
            ON CONFLICT (""VariantId"", ""WarehouseId"")
            DO UPDATE SET ""Quantity"" = EXCLUDED.""Quantity"",
                          ""Revision"" = ""VariantStock"".""Revision"" + 1
             WHERE ""VariantStock"".""ReservedQuantity"" <= EXCLUDED.""Quantity""",
            new { v = variantId, w = warehouseId, q = qty }, cancellationToken: ct)).ConfigureAwait(false);

        return rows == 0
            ? RowResult.Err("qty below reserved quantity (would violate non-negative invariant)")
            : RowResult.Ok;
    }
}
