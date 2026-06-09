using System.Text.Json;
using dCMS.Order.Core.Cart;
using StackExchange.Redis;

namespace dCMS.Order.Infrastructure.Cart;

public sealed class RedisCartStore(IConnectionMultiplexer redis) : ICartStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly TimeSpan CartTtl = TimeSpan.FromDays(7);

    public async Task<CartSnapshot?> GetAsync(
        string tenantId,
        string storeId,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(Key(tenantId, storeId, ownerId)).ConfigureAwait(false);
        if (raw.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<CartSnapshot>(raw!, JsonOptions);
    }

    public async Task<CartSnapshot> UpsertLineAsync(
        string tenantId,
        string storeId,
        string ownerId,
        UpsertCartLineRequest line,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);
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

        return await SaveAsync(tenantId, storeId, ownerId, lines, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CartSnapshot?> UpdateLineQuantityAsync(
        string tenantId,
        string storeId,
        string ownerId,
        string lineId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return null;

        var lines = existing.Lines.ToList();
        var idx = lines.FindIndex(l => string.Equals(l.LineId, lineId, StringComparison.Ordinal));
        if (idx < 0)
            return null;

        if (quantity <= 0)
        {
            lines.RemoveAt(idx);
            if (lines.Count == 0)
            {
                await ClearAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);
                return new CartSnapshot(tenantId, storeId, ownerId, [], DateTimeOffset.UtcNow);
            }
        }
        else
        {
            var old = lines[idx];
            lines[idx] = old with { Quantity = quantity };
        }

        return await SaveAsync(tenantId, storeId, ownerId, lines, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CartSnapshot?> RemoveLineAsync(
        string tenantId,
        string storeId,
        string ownerId,
        string lineId,
        CancellationToken cancellationToken = default) =>
        await UpdateLineQuantityAsync(tenantId, storeId, ownerId, lineId, 0, cancellationToken).ConfigureAwait(false);

    public Task ClearAsync(string tenantId, string storeId, string ownerId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        return db.KeyDeleteAsync(Key(tenantId, storeId, ownerId));
    }

    private async Task<CartSnapshot> SaveAsync(
        string tenantId,
        string storeId,
        string ownerId,
        List<CartLine> lines,
        CancellationToken cancellationToken)
    {
        var snapshot = new CartSnapshot(tenantId, storeId, ownerId, lines, DateTimeOffset.UtcNow);
        var db = redis.GetDatabase();
        await db.StringSetAsync(
                Key(tenantId, storeId, ownerId),
                JsonSerializer.Serialize(snapshot, JsonOptions),
                CartTtl)
            .ConfigureAwait(false);
        return snapshot;
    }

    private static RedisKey Key(string tenantId, string storeId, string ownerId) =>
        $"dcms:cart:{tenantId}:{storeId}:{ownerId}";
}
