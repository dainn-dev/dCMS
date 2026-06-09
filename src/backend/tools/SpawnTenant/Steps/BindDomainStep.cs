using dCMS.Provisioning.Domain;
using StackExchange.Redis;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class BindDomainStep(ITenantProvisioningRepository repository) : IProvisioningStep
{
    public string Name => ProvisioningStepNames.BindDomain;
    public int Order => 8;
    public int MaxRetries => 2;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        if (string.IsNullOrWhiteSpace(ctx.PrimaryDomain))
        {
            ctx.MarkStepCompleted(Name);
            return;
        }

        var domain = ctx.PrimaryDomain.Trim().ToLowerInvariant();
        var redisKey = $"dcms:host:{domain}";
        var redisValue = $"{ctx.TenantId}|{ctx.DefaultStoreId}";

        if (!string.IsNullOrWhiteSpace(ctx.RedisConnectionString))
        {
            var redis = await ConnectionMultiplexer.ConnectAsync(ctx.RedisConnectionString).ConfigureAwait(false);
            await redis.GetDatabase().StringSetAsync(redisKey, redisValue).ConfigureAwait(false);
            ctx.StepArtifacts["redis_host_key"] = redisKey;
        }

        await repository.UpsertDomainBindingAsync(
            domain, ctx.TenantId, ctx.DefaultStoreId, isPrimary: true,
            DomainBindingStatus.Active, redisKey, $"[\"{redisKey}\"]", ctx.CancellationToken)
            .ConfigureAwait(false);

        ctx.MarkStepCompleted(Name);
    }

    public async Task RollbackAsync(ProvisioningContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.PrimaryDomain))
            return;

        var domain = ctx.PrimaryDomain.Trim().ToLowerInvariant();
        if (ctx.StepArtifacts.TryGetValue("redis_host_key", out var keyObj) &&
            keyObj is string redisKey &&
            !string.IsNullOrWhiteSpace(ctx.RedisConnectionString))
        {
            try
            {
                var redis = await ConnectionMultiplexer.ConnectAsync(ctx.RedisConnectionString).ConfigureAwait(false);
                await redis.GetDatabase().KeyDeleteAsync(redisKey).ConfigureAwait(false);
            }
            catch { /* best effort */ }
        }

        await repository.UpsertDomainBindingAsync(
            domain, ctx.TenantId, ctx.DefaultStoreId, isPrimary: true,
            DomainBindingStatus.Removed, null, "[]", ctx.CancellationToken).ConfigureAwait(false);
    }
}
