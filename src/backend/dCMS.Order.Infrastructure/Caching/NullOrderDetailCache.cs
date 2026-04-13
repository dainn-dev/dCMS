namespace dCMS.Order.Infrastructure.Caching;

public sealed class NullOrderDetailCache : IOrderDetailCache
{
    public Task<string?> GetDetailJsonAsync(string orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task SetDetailJsonAsync(string orderId, string json, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task InvalidateAsync(string orderId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
