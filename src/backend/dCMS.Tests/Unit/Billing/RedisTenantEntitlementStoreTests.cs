using dCMS.Billing.Domain;
using dCMS.Infrastructure.Billing;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace dCMS.Tests.Unit.Billing;

public sealed class RedisTenantEntitlementStoreTests : IAsyncLifetime
{
    private RedisContainer? _redis;
    private IConnectionMultiplexer? _mux;

    public async Task InitializeAsync()
    {
        _redis = new RedisBuilder().Build();
        await _redis.StartAsync();
        _mux = ConnectionMultiplexer.Connect(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_mux is not null)
            await _mux.DisposeAsync();
        if (_redis is not null)
            await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Publish_then_TryGet_returns_snapshot()
    {
        var store = CreateStore();
        var version = await store.BumpVersionAsync("t-redis");
        var snapshot = TenantEntitlementSnapshot.Create(
            "t-redis", PlanCode.Pro, TenantSubscriptionState.Active, ManualInvoiceStatus.Paid,
            tenantActive: true, null, 10, 5000, ["catalog.write"], version: version);

        await store.PublishAsync(snapshot);
        var read = await store.TryGetAsync("t-redis");
        read.Should().NotBeNull();
        read!.PlanCode.Should().Be(PlanCode.Pro);
        read.MaxActiveProducts.Should().Be(5000);
    }

    [Fact]
    public async Task BumpVersion_invalidates_previous_payload()
    {
        var store = CreateStore();
        var v1Version = await store.BumpVersionAsync("t-rev");
        var v1 = TenantEntitlementSnapshot.Create(
            "t-rev", PlanCode.Starter, TenantSubscriptionState.Active, ManualInvoiceStatus.None,
            tenantActive: true, null, 2, 500, ["catalog.write"], version: v1Version);
        await store.PublishAsync(v1);

        var v2Version = await store.BumpVersionAsync("t-rev");
        var v2 = TenantEntitlementSnapshot.Create(
            "t-rev", PlanCode.Starter, TenantSubscriptionState.Suspended, ManualInvoiceStatus.None,
            tenantActive: true, null, 2, 500, ["catalog.write"], version: v2Version);
        await store.PublishAsync(v2);

        var read = await store.TryGetAsync("t-rev");
        read!.SubscriptionState.Should().Be(TenantSubscriptionState.Suspended);
    }

    private RedisTenantEntitlementStore CreateStore()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new TenantEntitlementCacheOptions());
        return new RedisTenantEntitlementStore(_mux, memory, options, NullLogger<RedisTenantEntitlementStore>.Instance);
    }
}
