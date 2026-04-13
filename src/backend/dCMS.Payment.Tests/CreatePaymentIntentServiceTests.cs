using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure;
using dCMS.Payment.Infrastructure.Persistence;
using Moq;

namespace dCMS.Payment.Tests;

public sealed class CreatePaymentIntentServiceTests
{
    [Fact]
    public async Task ExecuteAsync_calls_gateway_then_persists_initiated_row()
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway
            .Setup(g => g.CreateIntentAsync(It.IsAny<CreatePaymentIntentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentGatewayIntent("pi_test", "https://checkout.example/pay"));

        PaymentTransactionInsert? captured = null;
        var repo = new Mock<IPaymentTransactionRepository>();
        repo
            .Setup(r => r.InsertInitiatedAsync(It.IsAny<PaymentTransactionInsert>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentTransactionInsert, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        var sut = new CreatePaymentIntentService(gateway.Object, repo.Object);
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        var outcome = await sut.ExecuteAsync(
            orderId.ToString("D"),
            tenantId.ToString("D"),
            storeId.ToString("D"),
            "customer-1",
            99.5m,
            "USD",
            "card");

        var success = Assert.IsType<CreatePaymentIntentOutcome.Success>(outcome);
        Assert.Equal("pi_test", success.PaymentIntentId);
        Assert.Equal("https://checkout.example/pay", success.PaymentUrl);

        Assert.NotNull(captured);
        Assert.Equal(orderId, captured!.OrderId);
        Assert.Equal(tenantId, captured.TenantId);
        Assert.Equal(storeId, captured.StoreId);
        Assert.Equal("customer-1", captured.CustomerId);
        Assert.Equal("card", captured.PaymentMethod);
        Assert.Equal("pi_test", captured.PaymentIntentId);
        Assert.Equal(99.5m, captured.Amount);
        Assert.Equal("USD", captured.Currency);
        Assert.Equal("stub", captured.Provider);

        gateway.Verify(
            g => g.CreateIntentAsync(
                It.Is<CreatePaymentIntentGatewayRequest>(r =>
                    r.OrderId == orderId
                    && r.TenantId == tenantId
                    && r.StoreId == storeId
                    && r.CustomerId == "customer-1"
                    && r.Amount == 99.5m
                    && r.Currency == "USD"
                    && r.PaymentMethod == "card"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repo.Verify(
            r => r.InsertInitiatedAsync(It.IsAny<PaymentTransactionInsert>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_when_order_id_invalid_returns_validation_error()
    {
        var gateway = new Mock<IPaymentGateway>();
        var repo = new Mock<IPaymentTransactionRepository>();
        var sut = new CreatePaymentIntentService(gateway.Object, repo.Object);

        var outcome = await sut.ExecuteAsync(
            "not-a-guid",
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            "c",
            1m,
            "USD",
            "card");

        var err = Assert.IsType<CreatePaymentIntentOutcome.ValidationError>(outcome);
        Assert.Equal("INVALID_ORDER", err.Code);
        gateway.Verify(
            g => g.CreateIntentAsync(It.IsAny<CreatePaymentIntentGatewayRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
