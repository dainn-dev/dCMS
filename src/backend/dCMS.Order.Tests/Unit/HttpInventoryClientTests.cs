using System.Net;
using System.Text;
using dCMS.Order.Core.Integration;
using dCMS.Order.Infrastructure.Integration;
using Microsoft.Extensions.Logging.Abstractions;

namespace dCMS.Order.Tests.Unit;

public sealed class HttpInventoryClientTests
{
    [Fact]
    public async Task EnsureStockAvailableAsync_when_sufficient_completes()
    {
        using var handler = new StubHandler(_ => ResponseEnvelope(sufficient: true, available: 10, requested: 2));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://inventory.test/") };
        var sut = new HttpInventoryClient(http, NullLogger<HttpInventoryClient>.Instance);

        await sut.EnsureStockAvailableAsync("t1", "s1", [new InventoryCheckLine("v1", "w1", 2)]);
    }

    [Fact]
    public async Task EnsureStockAvailableAsync_when_not_sufficient_throws_OutOfStockException()
    {
        using var handler = new StubHandler(_ => ResponseEnvelope(sufficient: false, available: 1, requested: 5));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://inventory.test/") };
        var sut = new HttpInventoryClient(http, NullLogger<HttpInventoryClient>.Instance);

        var ex = await Assert.ThrowsAsync<OutOfStockException>(() =>
            sut.EnsureStockAvailableAsync("t1", "s1", [new InventoryCheckLine("v1", "w1", 5)]));

        Assert.Equal("v1", ex.VariantId);
        Assert.Equal("w1", ex.WarehouseId);
        Assert.Equal(5, ex.Requested);
        Assert.Equal(1, ex.Available);
    }

    [Fact]
    public async Task EnsureStockAvailableAsync_empty_lines_no_http_call()
    {
        var called = false;
        using var handler = new StubHandler(_ =>
        {
            called = true;
            return ResponseEnvelope(true, 0, 0);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://inventory.test/") };
        var sut = new HttpInventoryClient(http, NullLogger<HttpInventoryClient>.Instance);

        await sut.EnsureStockAvailableAsync("t1", "s1", []);
        Assert.False(called);
    }

    private static HttpResponseMessage ResponseEnvelope(bool sufficient, int available, int requested)
    {
        var suff = sufficient ? "true" : "false";
        var json = $"{{\"data\":{{\"sufficient\":{suff},\"available\":{available},\"requested\":{requested}}},\"error\":null}}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_send(request));
    }
}
