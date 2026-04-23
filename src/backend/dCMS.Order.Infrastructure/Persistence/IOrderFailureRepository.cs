namespace dCMS.Order.Infrastructure.Persistence;

public interface IOrderFailureRepository
{
    Task<IReadOnlyList<OrderFailureRow>> ListAsync(
        string tenantId,
        string storeId,
        string? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<OrderFailureRow?> GetAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task UpsertFailureAsync(
        OrderFailureRow row,
        string logEntryJson,
        CancellationToken cancellationToken = default);

    Task<bool> MarkResolvedAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        string resolvedBy,
        CancellationToken cancellationToken = default);

    Task<bool> IncrementRetryAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}

