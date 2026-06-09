using System.Collections.Concurrent;
using dCMS.Order.Core.Cart;

namespace dCMS.Order.Infrastructure.Cart;

/// <summary>Fallback when Redis is not configured (local dev/tests).</summary>
public sealed class InMemoryCartStore : ICartStore
{
    private readonly ConcurrentDictionary<string, CartSnapshot> _carts = new(StringComparer.Ordinal);

    public Task<CartSnapshot?> GetAsync(string tenantId, string storeId, string ownerId, CancellationToken cancellationToken = default)
    {
        _carts.TryGetValue(Key(tenantId, storeId, ownerId), out var cart);
        return Task.FromResult(cart);
    }

    public Task<CartSnapshot> UpsertLineAsync(
        string tenantId,
        string storeId,
        string ownerId,
        UpsertCartLineRequest line,
        CancellationToken cancellationToken = default)
    {
        _carts.TryGetValue(Key(tenantId, storeId, ownerId), out var existing);
        var lines = existing?.Lines.ToList() ?? [];
        var lineId = string.IsNullOrWhiteSpace(line.LineId) ? Guid.NewGuid().ToString("N")[..16] : line.LineId.Trim();
        var cartLine = new CartLine(
            lineId,
            line.ProductId.Trim(),
            line.VariantId.Trim(),
            line.WarehouseId.Trim(),
            line.Quantity,
            line.UnitPriceAmount,
            line.Currency.Trim(),
            line.ProductNameSnapshot,
            line.VariantSnapshotJson);

        var idx = lines.FindIndex(l => string.Equals(l.LineId, lineId, StringComparison.Ordinal));
        if (idx >= 0)
            lines[idx] = cartLine;
        else
            lines.Add(cartLine);

        var snapshot = new CartSnapshot(tenantId, storeId, ownerId, lines, DateTimeOffset.UtcNow);
        _carts[Key(tenantId, storeId, ownerId)] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<CartSnapshot?> UpdateLineQuantityAsync(
        string tenantId,
        string storeId,
        string ownerId,
        string lineId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (!_carts.TryGetValue(Key(tenantId, storeId, ownerId), out var existing))
            return Task.FromResult<CartSnapshot?>(null);

        var lines = existing.Lines.ToList();
        var idx = lines.FindIndex(l => string.Equals(l.LineId, lineId, StringComparison.Ordinal));
        if (idx < 0)
            return Task.FromResult<CartSnapshot?>(null);

        if (quantity <= 0)
            lines.RemoveAt(idx);
        else
            lines[idx] = lines[idx] with { Quantity = quantity };

        if (lines.Count == 0)
        {
            _carts.TryRemove(Key(tenantId, storeId, ownerId), out _);
            return Task.FromResult<CartSnapshot?>(new CartSnapshot(tenantId, storeId, ownerId, [], DateTimeOffset.UtcNow));
        }

        var updated = existing with { Lines = lines, UpdatedAt = DateTimeOffset.UtcNow };
        _carts[Key(tenantId, storeId, ownerId)] = updated;
        return Task.FromResult<CartSnapshot?>(updated);
    }

    public Task<CartSnapshot?> RemoveLineAsync(
        string tenantId,
        string storeId,
        string ownerId,
        string lineId,
        CancellationToken cancellationToken = default) =>
        UpdateLineQuantityAsync(tenantId, storeId, ownerId, lineId, 0, cancellationToken);

    public Task ClearAsync(string tenantId, string storeId, string ownerId, CancellationToken cancellationToken = default)
    {
        _carts.TryRemove(Key(tenantId, storeId, ownerId), out _);
        return Task.CompletedTask;
    }

    private static string Key(string tenantId, string storeId, string ownerId) => $"{tenantId}:{storeId}:{ownerId}";
}
