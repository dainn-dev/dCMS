using dCMS.Core.Exceptions;
using dCMS.Inventory.Commands;
using dCMS.Core.Messaging;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Models;
using dCMS.Inventory.Persistence;

namespace dCMS.Inventory.Services;

public sealed class StockService(IInventoryStockPersistence persistence)
{
    private readonly IInventoryStockPersistence _persistence = persistence;

    public Task AdjustStockAsync(AdjustStockCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            command.TenantId,
            command.StoreId,
            command.VariantId,
            command.WarehouseId,
            stock =>
            {
                stock.Adjust(command.Delta);
                return StockMovement.ForAppend(command.VariantId, command.WarehouseId, command.Delta,
                    StockMovementType.Adjustment, command.CreatedBy, command.ReferenceId, now);
            },
            cancellationToken);

    public Task ReserveStockAsync(ReserveStockCommand command, DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            command.TenantId,
            command.StoreId,
            command.VariantId,
            command.WarehouseId,
            stock =>
            {
                stock.Reserve(command.Quantity);
                return StockMovement.ForAppend(command.VariantId, command.WarehouseId, -command.Quantity,
                    StockMovementType.Order, command.CreatedBy, command.ReferenceId, now);
            },
            cancellationToken);

    public Task ReleaseStockAsync(ReleaseStockCommand command, DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            command.TenantId,
            command.StoreId,
            command.VariantId,
            command.WarehouseId,
            stock =>
            {
                stock.Release(command.Quantity);
                return StockMovement.ForAppend(command.VariantId, command.WarehouseId, command.Quantity,
                    StockMovementType.Cancel, command.CreatedBy, command.ReferenceId, now);
            },
            cancellationToken);

    private async Task ExecuteWithRetryAsync(
        string tenantId,
        string storeId,
        string variantId,
        string warehouseId,
        Func<VariantStock, StockMovement> buildMovement,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        StockConcurrencyException? lastConcurrency = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var stock = await _persistence
                    .GetStockAsync(tenantId, storeId, variantId, warehouseId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new VariantStockNotFoundException(variantId, warehouseId);

                var movement = buildMovement(stock);
                var envelope = new StockUpdatedV1(stock.VariantId, stock.WarehouseId, tenantId, storeId, stock.Quantity,
                    stock.ReservedQuantity, movement.CreatedAt);

                await _persistence
                    .CommitStockChangeAsync(tenantId, storeId, stock, movement, envelope, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (StockConcurrencyException ex)
            {
                lastConcurrency = ex;
                if (attempt >= maxRetries)
                    break;
                await Task.Delay(50 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastConcurrency ?? new StockConcurrencyException(variantId, warehouseId);
    }
}
