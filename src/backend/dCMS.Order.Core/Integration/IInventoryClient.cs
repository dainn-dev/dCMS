namespace dCMS.Order.Core.Integration;

/// <summary>Sync inventory availability (DAI-314). Calls Inventory Service <c>POST /internal/inventory/check</c> per line.</summary>
public interface IInventoryClient
{
    /// <summary>
    /// Ensures each line has enough stock. Throws <see cref="OutOfStockException"/> when Inventory returns <c>sufficient: false</c>.
    /// </summary>
    Task EnsureStockAvailableAsync(
        string tenantId,
        string storeId,
        IReadOnlyList<InventoryCheckLine> lines,
        CancellationToken cancellationToken = default);
}
