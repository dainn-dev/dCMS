using dCMS.Provisioning.Domain;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Platform;

public interface IDomainBindingRedisSync
{
    Task SyncActiveBindingAsync(string domain, string tenantId, string storeId, CancellationToken cancellationToken = default);

    Task RemoveBindingAsync(string domain, CancellationToken cancellationToken = default);
}

public sealed class DomainBindingRedisSync(IConnectionMultiplexer? redis) : IDomainBindingRedisSync
{
    public async Task SyncActiveBindingAsync(
        string domain,
        string tenantId,
        string storeId,
        CancellationToken cancellationToken = default)
    {
        if (redis is null)
            return;

        var key = $"dcms:host:{domain.Trim().ToLowerInvariant()}";
        await redis.GetDatabase()
            .StringSetAsync(key, $"{tenantId}|{storeId}")
            .ConfigureAwait(false);
    }

    public async Task RemoveBindingAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (redis is null)
            return;

        await redis.GetDatabase()
            .KeyDeleteAsync($"dcms:host:{domain.Trim().ToLowerInvariant()}")
            .ConfigureAwait(false);
    }
}
