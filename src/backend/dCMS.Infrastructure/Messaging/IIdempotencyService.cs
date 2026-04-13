namespace dCMS.Infrastructure.Messaging;

/// <summary>US-F1 / DAI-347 — deduplicate broker deliveries by transport <c>MessageId</c>.</summary>
public interface IIdempotencyService
{
    /// <summary>PostgreSQL <c>pg_advisory_lock</c> for the same message id (cross-instance). Dispose to unlock.</summary>
    Task<IAsyncDisposable> AcquireOrderingLockAsync(string messageId, CancellationToken cancellationToken = default);

    Task<bool> IsProcessedAsync(string messageId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default);
}
