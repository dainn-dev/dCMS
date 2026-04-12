using System.Collections.Concurrent;
using MediatR;

namespace dCMS.Catalog.Worker.Indexing;

/// <summary>
/// Trailing-edge debounce (~500ms) per (tenant, store, product) for inventory <c>StockUpdated</c> reindex bursts (catalog spec).
/// </summary>
public sealed class DebouncedStockProductIndexPublisher(
    IMediator mediator,
    ILogger<DebouncedStockProductIndexPublisher> logger,
    IHostApplicationLifetime lifetime)
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);
    private readonly ConcurrentDictionary<string, Gate> _gates = new(StringComparer.Ordinal);

    public void ScheduleUpsert(string tenantId, string storeId, string productId)
    {
        var key = $"{tenantId}:{storeId}:{productId}";
        var gate = _gates.GetOrAdd(key, _ => new Gate());
        CancellationTokenSource linked;
        lock (gate)
        {
            gate.LinkedCts?.Cancel();
            gate.LinkedCts?.Dispose();
            linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            gate.LinkedCts = linked;
        }

        _ = RunDelayedAsync(key, tenantId, storeId, productId, gate, linked);
    }

    private async Task RunDelayedAsync(string key, string tenantId, string storeId, string productId, Gate gate,
        CancellationTokenSource myLinked)
    {
        try
        {
            await Task.Delay(DebounceDelay, myLinked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (gate)
        {
            if (!ReferenceEquals(gate.LinkedCts, myLinked))
                return;
            gate.LinkedCts = null;
        }

        _gates.TryRemove(key, out _);
        myLinked.Dispose();

        try
        {
            await mediator
                .Publish(new UpsertProductSearchIndexNotification(productId, tenantId, storeId), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Debounced ES upsert failed for product {ProductId} tenant {TenantId} store {StoreId}.",
                productId, tenantId, storeId);
        }
    }

    private sealed class Gate
    {
        public CancellationTokenSource? LinkedCts;
    }
}
