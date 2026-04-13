namespace dCMS.Order.Infrastructure.Caching;

/// <summary>US-21 — Redis cache-aside for GET order detail (<c>dcms:order:{orderId}</c>,60s TTL).</summary>
public interface IOrderDetailCache
{
    Task<string?> GetDetailJsonAsync(string orderId, CancellationToken cancellationToken = default);

    Task SetDetailJsonAsync(string orderId, string json, CancellationToken cancellationToken = default);

    Task InvalidateAsync(string orderId, CancellationToken cancellationToken = default);
}
