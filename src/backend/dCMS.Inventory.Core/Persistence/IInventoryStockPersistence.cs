using dCMS.Core.Messaging;
using dCMS.Inventory.Models;

namespace dCMS.Inventory.Persistence;

public interface IInventoryStockPersistence
{
    Task<VariantStock?> GetStockAsync(string tenantId, string storeId, string variantId, string warehouseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates <see cref="VariantStock"/> quantities, appends movement and outbox row in one transaction.
    /// Throws <see cref="dCMS.Core.Exceptions.StockConcurrencyException"/> when the persisted revision (<c>Revision</c> / <see cref="VariantStock.RowVersion"/>) does not match.
    /// </summary>
    Task CommitStockChangeAsync(string tenantId, string storeId, VariantStock stock, StockMovement movement,
        StockUpdatedV1 envelope, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseSummary>> ListWarehousesForStoreAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VariantWarehouseStock>> ListStockByVariantAsync(string tenantId, string storeId, string variantId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a warehouse row for the tenant/store. Throws <see cref="dCMS.Inventory.Exceptions.DuplicateWarehouseException"/> on id conflict.</summary>
    Task CreateWarehouseAsync(string tenantId, string storeId, string warehouseId, string name, string? address,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the absolute on-hand quantity for a variant/warehouse (creating the stock row if missing), appending a
    /// movement (delta vs. previous on-hand) and an outbox event in one transaction. Returns the resulting on-hand quantity.
    /// Throws <see cref="dCMS.Inventory.Exceptions.VariantStockNotFoundException"/> when the warehouse does not belong to the tenant/store,
    /// and <see cref="dCMS.Core.Exceptions.StockInvariantException"/> when the target is below the reserved quantity.
    /// </summary>
    Task<int> SetOnHandQuantityAsync(string tenantId, string storeId, string variantId, string warehouseId,
        int quantity, string createdBy, DateTimeOffset now, CancellationToken cancellationToken = default);
}
