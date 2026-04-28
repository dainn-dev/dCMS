using dCMS.Order.Infrastructure.Payments;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

public sealed class StubGatewayTenderClientTests
{
    [Fact]
    public async Task Authorize_default_succeeds_with_charge_ref()
    {
        var client = new StubGatewayTenderClient();
        var orderId = Guid.NewGuid();
        var r = await client.AuthorizeAsync("t1", "cust-1", orderId, 100m, "USD", default);
        Assert.True(r.Success);
        Assert.Equal($"ch_stub_{orderId:N}", r.ExternalRef);
    }

    [Fact]
    public async Task Authorize_decline_keyword_in_customer_fails()
    {
        var client = new StubGatewayTenderClient();
        var r = await client.AuthorizeAsync("t1", "cust-decline-me", Guid.NewGuid(), 100m, "USD", default);
        Assert.False(r.Success);
        Assert.Equal("card_declined", r.ErrorCode);
    }

    [Fact]
    public async Task Authorize_amount_ending_99_fails_insufficient_funds()
    {
        var client = new StubGatewayTenderClient();
        var r = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 12.99m, "USD", default);
        Assert.False(r.Success);
        Assert.Equal("insufficient_funds", r.ErrorCode);
    }

    [Fact]
    public async Task Authorize_timeout_keyword_throws()
    {
        var client = new StubGatewayTenderClient();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.AuthorizeAsync("t1", "cust-timeout", Guid.NewGuid(), 100m, "USD", default));
    }

    [Fact]
    public async Task Capture_then_capture_again_idempotent()
    {
        var client = new StubGatewayTenderClient();
        var orderId = Guid.NewGuid();
        var auth = await client.AuthorizeAsync("t1", "cust-1", orderId, 100m, "USD", default);
        Assert.True((await client.CaptureAsync("t1", auth.ExternalRef!, default)).Success);
        Assert.True((await client.CaptureAsync("t1", auth.ExternalRef!, default)).Success);
    }

    [Fact]
    public async Task Refund_idempotent()
    {
        var client = new StubGatewayTenderClient();
        var auth = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 100m, "USD", default);
        await client.CaptureAsync("t1", auth.ExternalRef!, default);
        Assert.True((await client.RefundAsync("t1", auth.ExternalRef!, default)).Success);
        Assert.True((await client.RefundAsync("t1", auth.ExternalRef!, default)).Success);
    }

    [Fact]
    public async Task Void_releases_uncaptured()
    {
        var client = new StubGatewayTenderClient();
        var auth = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 100m, "USD", default);
        var v = await client.VoidAsync("t1", auth.ExternalRef!, "abandoned", default);
        Assert.True(v.Success);
    }

    [Fact]
    public async Task Refund_uncaptured_fails()
    {
        var client = new StubGatewayTenderClient();
        var auth = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 100m, "USD", default);
        var r = await client.RefundAsync("t1", auth.ExternalRef!, default);
        Assert.False(r.Success);
        Assert.Equal("invalid_state", r.ErrorCode);
    }
}
