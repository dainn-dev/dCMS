using dCMS.Order.Core.Cart;
using dCMS.Order.Infrastructure.Cart;

namespace dCMS.Order.Tests.Unit.Cart;

public sealed class InMemoryCartStoreTests
{
    private readonly InMemoryCartStore _store = new();

    [Fact]
    public async Task Upsert_and_get_roundtrip()
    {
        var line = new UpsertCartLineRequest(
            null,
            "prod-1",
            "var-1",
            "wh-1",
            2,
            9.99m,
            "USD",
            "Widget",
            """{"sku":"W"}""");

        var cart = await _store.UpsertLineAsync("t1", "s1", "cust-1", line);
        Assert.Single(cart.Lines);

        var loaded = await _store.GetAsync("t1", "s1", "cust-1");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Lines[0].Quantity);
    }

    [Fact]
    public async Task Clear_removes_cart()
    {
        await _store.UpsertLineAsync("t1", "s1", "cust-1", new UpsertCartLineRequest(
            null, "p", "v", "w", 1, 1m, "USD", "P", "{}"));
        await _store.ClearAsync("t1", "s1", "cust-1");

        var loaded = await _store.GetAsync("t1", "s1", "cust-1");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Tenant_isolation()
    {
        await _store.UpsertLineAsync("t1", "s1", "cust-1", new UpsertCartLineRequest(
            null, "p", "v", "w", 1, 1m, "USD", "P", "{}"));

        var other = await _store.GetAsync("t2", "s1", "cust-1");
        Assert.Null(other);
    }
}
