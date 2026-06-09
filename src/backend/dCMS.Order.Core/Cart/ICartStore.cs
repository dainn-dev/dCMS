namespace dCMS.Order.Core.Cart;

public interface ICartStore
{
    Task<CartSnapshot?> GetAsync(string tenantId, string storeId, string ownerId, CancellationToken cancellationToken = default);

    Task<CartSnapshot> UpsertLineAsync(
        string tenantId,
        string storeId,
        string ownerId,
        UpsertCartLineRequest line,
        CancellationToken cancellationToken = default);

    Task<CartSnapshot?> UpdateLineQuantityAsync(
        string tenantId,
        string storeId,
        string ownerId,
        string lineId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<CartSnapshot?> RemoveLineAsync(
        string tenantId,
        string storeId,
        string ownerId,
        string lineId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(string tenantId, string storeId, string ownerId, CancellationToken cancellationToken = default);
}
