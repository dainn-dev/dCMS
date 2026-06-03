using System.Text.Json;
using Dapper;
using dCMS.Core.Exceptions;
using dCMS.Core.Messaging;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Models;
using dCMS.Inventory.Persistence;
using Npgsql;

namespace dCMS.Inventory.Infrastructure;

public sealed class SqlStockPersistence(string connectionString) : IInventoryStockPersistence
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<VariantStock?> GetStockAsync(string tenantId, string storeId, string variantId, string warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT vs."Id", vs."VariantId", vs."WarehouseId", vs."Quantity", vs."ReservedQuantity", vs."Revision" AS "RowVersion"
            FROM "VariantStock" vs
            INNER JOIN "Warehouses" w ON w."Id" = vs."WarehouseId"
            WHERE vs."VariantId" = @VariantId
              AND vs."WarehouseId" = @WarehouseId
              AND w."TenantId" = @TenantId
              AND w."StoreId" = @StoreId
            """;
        var row = await connection.QuerySingleOrDefaultAsync<StockRow>(
            new CommandDefinition(sql, new { VariantId = variantId, WarehouseId = warehouseId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null
            ? null
            : VariantStock.Restore(row.Id, row.VariantId, row.WarehouseId, row.Quantity, row.ReservedQuantity, row.RowVersion);
    }

    public async Task CommitStockChangeAsync(string tenantId, string storeId, VariantStock stock, StockMovement movement,
        StockUpdatedV1 envelope, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string updateSql = """
            UPDATE "VariantStock" vs
            SET "Quantity" = @Quantity,
                "ReservedQuantity" = @ReservedQuantity,
                "Revision" = vs."Revision" + 1
            FROM "Warehouses" w
            WHERE vs."WarehouseId" = w."Id"
              AND vs."VariantId" = @VariantId
              AND vs."WarehouseId" = @WarehouseId
              AND w."TenantId" = @TenantId
              AND w."StoreId" = @StoreId
              AND vs."Revision" = @RowVersion
            RETURNING vs."Revision"
            """;

        var newRevision = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(updateSql,
                new
                {
                    Quantity = stock.Quantity,
                    ReservedQuantity = stock.ReservedQuantity,
                    stock.VariantId,
                    stock.WarehouseId,
                    TenantId = tenantId,
                    StoreId = storeId,
                    RowVersion = stock.RowVersion
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (newRevision is null)
            throw new StockConcurrencyException(stock.VariantId, stock.WarehouseId);

        const string insertMovement = """
            INSERT INTO "StockMovements" ("VariantId", "WarehouseId", "Delta", "Type", "ReferenceId", "CreatedAt", "CreatedBy")
            VALUES (@VariantId, @WarehouseId, @Delta, @Type, @ReferenceId, @CreatedAt, @CreatedBy)
            """;

        await connection.ExecuteAsync(new CommandDefinition(insertMovement,
            new
            {
                movement.VariantId,
                movement.WarehouseId,
                movement.Delta,
                Type = movement.Type.ToString(),
                movement.ReferenceId,
                movement.CreatedAt,
                movement.CreatedBy
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(envelope);
        const string insertOutbox = """
            INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt")
            VALUES (@EventType, @Payload, @CreatedAt)
            """;

        await connection.ExecuteAsync(new CommandDefinition(insertOutbox,
            new { EventType = "StockUpdated.v1", Payload = payload, CreatedAt = envelope.OccurredAt }, tx,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        stock.RowVersion = newRevision.Value;

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WarehouseSummary>> ListWarehousesForStoreAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "Id", "Name", "Address", "IsActive"
            FROM "Warehouses"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId
            ORDER BY "Id"
            """;
        var rows = await connection.QueryAsync<WarehouseRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new WarehouseSummary(r.Id, r.Name, r.Address, r.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<VariantWarehouseStock>> ListStockByVariantAsync(string tenantId, string storeId,
        string variantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT vs."WarehouseId", w."Name" AS WarehouseName, vs."Quantity", vs."ReservedQuantity"
            FROM "VariantStock" vs
            INNER JOIN "Warehouses" w ON w."Id" = vs."WarehouseId"
            WHERE vs."VariantId" = @VariantId
              AND w."TenantId" = @TenantId
              AND w."StoreId" = @StoreId
            ORDER BY vs."WarehouseId"
            """;
        var rows = await connection.QueryAsync<VariantStockRow>(
            new CommandDefinition(sql, new { VariantId = variantId, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new VariantWarehouseStock(r.WarehouseId, r.WarehouseName, r.Quantity, r.ReservedQuantity))
            .ToList();
    }

    public async Task CreateWarehouseAsync(string tenantId, string storeId, string warehouseId, string name, string? address,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "Warehouses" ("Id", "TenantId", "StoreId", "Name", "Address", "IsActive")
            VALUES (@Id, @TenantId, @StoreId, @Name, @Address, TRUE)
            """;
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql,
                new { Id = warehouseId, TenantId = tenantId, StoreId = storeId, Name = name, Address = address },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new DuplicateWarehouseException(warehouseId);
        }
    }

    public async Task<int> SetOnHandQuantityAsync(string tenantId, string storeId, string variantId, string warehouseId,
        int quantity, string createdBy, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (quantity < 0)
            throw new StockInvariantException("Quantity must be zero or positive.");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string warehouseSql = """
            SELECT 1 FROM "Warehouses" WHERE "Id" = @WarehouseId AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var warehouseExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(warehouseSql,
            new { WarehouseId = warehouseId, TenantId = tenantId, StoreId = storeId }, tx,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (warehouseExists is null)
            throw new VariantStockNotFoundException(variantId, warehouseId);

        const string currentSql = """
            SELECT "Quantity", "ReservedQuantity"
            FROM "VariantStock"
            WHERE "VariantId" = @VariantId AND "WarehouseId" = @WarehouseId
            """;
        var current = await connection.QuerySingleOrDefaultAsync<(int Quantity, int ReservedQuantity)?>(
            new CommandDefinition(currentSql, new { VariantId = variantId, WarehouseId = warehouseId }, tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        var previousQuantity = current?.Quantity ?? 0;
        var reserved = current?.ReservedQuantity ?? 0;
        if (quantity < reserved)
            throw new StockInvariantException(
                $"Cannot set on-hand to {quantity}: {reserved} unit(s) are currently reserved.");

        if (current is null)
        {
            const string insertStock = """
                INSERT INTO "VariantStock" ("VariantId", "WarehouseId", "Quantity", "ReservedQuantity", "Revision")
                VALUES (@VariantId, @WarehouseId, @Quantity, 0, 1)
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertStock,
                new { VariantId = variantId, WarehouseId = warehouseId, Quantity = quantity }, tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        else
        {
            const string updateStock = """
                UPDATE "VariantStock"
                SET "Quantity" = @Quantity, "Revision" = "Revision" + 1
                WHERE "VariantId" = @VariantId AND "WarehouseId" = @WarehouseId
                """;
            await connection.ExecuteAsync(new CommandDefinition(updateStock,
                new { VariantId = variantId, WarehouseId = warehouseId, Quantity = quantity }, tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        var delta = quantity - previousQuantity;
        const string insertMovement = """
            INSERT INTO "StockMovements" ("VariantId", "WarehouseId", "Delta", "Type", "ReferenceId", "CreatedAt", "CreatedBy")
            VALUES (@VariantId, @WarehouseId, @Delta, 'Adjustment', @ReferenceId, @CreatedAt, @CreatedBy)
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertMovement,
            new
            {
                VariantId = variantId,
                WarehouseId = warehouseId,
                Delta = delta,
                ReferenceId = "set-on-hand",
                CreatedAt = now,
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "api" : createdBy
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var envelope = new StockUpdatedV1(variantId, warehouseId, tenantId, storeId, quantity, reserved, now);
        var payload = JsonSerializer.Serialize(envelope);
        const string insertOutbox = """
            INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt")
            VALUES (@EventType, @Payload, @CreatedAt)
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertOutbox,
            new { EventType = "StockUpdated.v1", Payload = payload, CreatedAt = envelope.OccurredAt }, tx,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return quantity;
    }

    private sealed class StockRow
    {
        public int Id { get; init; }
        public string VariantId { get; init; } = null!;
        public string WarehouseId { get; init; } = null!;
        public int Quantity { get; init; }
        public int ReservedQuantity { get; init; }
        public long RowVersion { get; init; }
    }

    private sealed class WarehouseRow
    {
        public string Id { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string? Address { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class VariantStockRow
    {
        public string WarehouseId { get; init; } = null!;
        public string WarehouseName { get; init; } = null!;
        public int Quantity { get; init; }
        public int ReservedQuantity { get; init; }
    }
}
