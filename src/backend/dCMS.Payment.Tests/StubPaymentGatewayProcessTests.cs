using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Integration;

namespace dCMS.Payment.Tests;

public sealed class StubPaymentGatewayProcessTests
{
    [Fact]
    public async Task ProcessPayment_second_call_with_same_intent_is_AlreadySucceeded()
    {
        var gateway = new StubPaymentGateway();
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var req = new ProcessPaymentGatewayRequest("pi_same", orderId, tenantId, 5m, "USD", "card");

        var first = await gateway.ProcessPaymentAsync(req);
        var second = await gateway.ProcessPaymentAsync(req);

        Assert.IsType<ProcessPaymentGatewayResult.Succeeded>(first);
        Assert.IsType<ProcessPaymentGatewayResult.AlreadySucceeded>(second);
    }

    [Fact]
    public async Task ProcessPayment_when_intent_contains_decline_returns_Failed()
    {
        var gateway = new StubPaymentGateway();
        var req = new ProcessPaymentGatewayRequest(
            "pi_decline_test",
            Guid.NewGuid(),
            Guid.NewGuid(),
            5m,
            "USD",
            "card");

        var result = await gateway.ProcessPaymentAsync(req);

        var failed = Assert.IsType<ProcessPaymentGatewayResult.Failed>(result);
        Assert.Equal("card_declined", failed.ErrorCode);
    }
}
