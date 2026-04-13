using dCMS.Core.Messaging;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Messaging;
using dCMS.Payment.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace dCMS.Payment.Tests;

public sealed class RefundPaymentConsumerTests
{
    private static void ConfigureHarness(IBusRegistrationConfigurator cfg) => cfg.AddConsumer<RefundPaymentConsumer>();

    private static PaymentTransaction Row(
        Guid orderId,
        Guid tenantId,
        Guid storeId,
        PaymentTransactionStatus status = PaymentTransactionStatus.Succeeded,
        string intentId = "pi_r1") =>
        new(
            Guid.NewGuid(),
            orderId,
            tenantId,
            storeId,
            "cust",
            "card",
            intentId,
            10m,
            "USD",
            status,
            "stub",
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Consume_when_succeeded_refunds_and_publishes_PaymentRefunded()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId);

        var gateway = new Mock<IPaymentGateway>();
        gateway
            .Setup(g => g.RefundPaymentAsync(It.IsAny<RefundPaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundPaymentGatewayResult.Succeeded("re_1"));

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(gateway.Object)
            .AddSingleton(repo.Object)
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(
                new RefundPaymentV1(
                    orderId.ToString("D"),
                    tenantId.ToString("D"),
                    storeId.ToString("D"),
                    10m,
                    "USD",
                    "late_payment_on_cancelled",
                    DateTimeOffset.UtcNow));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<PaymentRefundedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(row.Id, "refunded", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_when_already_refunded_in_db_republishes_PaymentRefunded_without_gateway()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId, PaymentTransactionStatus.Refunded);

        var gateway = new Mock<IPaymentGateway>();
        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(gateway.Object)
            .AddSingleton(repo.Object)
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(
                new RefundPaymentV1(
                    orderId.ToString("D"),
                    tenantId.ToString("D"),
                    storeId.ToString("D"),
                    10m,
                    "USD",
                    "late_payment_on_cancelled",
                    DateTimeOffset.UtcNow));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<PaymentRefundedV1>());
            gateway.Verify(
                g => g.RefundPaymentAsync(It.IsAny<RefundPaymentGatewayRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
