using dCMS.Core.Messaging;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Messaging;
using dCMS.Payment.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace dCMS.Payment.Tests;

public sealed class ProcessPaymentConsumerTests
{
    private static void ConfigureHarness(IBusRegistrationConfigurator cfg) => cfg.AddConsumer<ProcessPaymentConsumer>();

    private static PaymentTransaction Row(
        Guid orderId,
        Guid tenantId,
        Guid storeId,
        PaymentTransactionStatus status = PaymentTransactionStatus.Initiated,
        string intentId = "pi_1") =>
        new(
            Guid.NewGuid(),
            orderId,
            tenantId,
            storeId,
            "aeon",
            "cust",
            "card",
            intentId,
            10m,
            "USD",
            status,
            "stub",
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Consume_when_gateway_succeeds_publishes_PaymentCompleted_and_updates_row()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId);

        var gateway = new Mock<IPaymentGateway>();
        gateway
            .Setup(g => g.ProcessPaymentAsync(It.IsAny<ProcessPaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessPaymentGatewayResult.Succeeded("ch_1"));

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByOrderIdAsync(orderId, tenantId, "aeon", "stub", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(gateway.Object)
            .AddSingleton(repo.Object)
            .AddSingleton<IConfiguration>(TestConfiguration())
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(
                new ProcessPaymentV1(
                    Guid.NewGuid(),
                    orderId.ToString("D"),
                    tenantId.ToString("D"),
                    "cust",
                    10m,
                    "USD",
                    "card",
                    DateTimeOffset.UtcNow.AddMinutes(1)));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<PaymentCompletedV1>());
            Assert.False(await harness.Published.Any<PaymentFailedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(row.Id, tenantId, storeId, "aeon", "stub", "completed", It.IsAny<CancellationToken>()), Times.Once);
            gateway.Verify(
                g => g.ProcessPaymentAsync(
                    It.Is<ProcessPaymentGatewayRequest>(p =>
                        p.PaymentIntentId == row.PaymentIntentId
                        && p.OrderId == orderId
                        && p.TenantId == tenantId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_when_gateway_returns_AlreadySucceeded_marks_completed_and_publishes_PaymentCompleted()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId);

        var gateway = new Mock<IPaymentGateway>();
        gateway
            .Setup(g => g.ProcessPaymentAsync(It.IsAny<ProcessPaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessPaymentGatewayResult.AlreadySucceeded("ch_dup"));

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByOrderIdAsync(orderId, tenantId, "aeon", "stub", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(gateway.Object)
            .AddSingleton(repo.Object)
            .AddSingleton<IConfiguration>(TestConfiguration())
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(
                new ProcessPaymentV1(
                    Guid.NewGuid(),
                    orderId.ToString("D"),
                    tenantId.ToString("D"),
                    "cust",
                    10m,
                    "USD",
                    "card",
                    DateTimeOffset.UtcNow.AddMinutes(1)));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<PaymentCompletedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(row.Id, tenantId, storeId, "aeon", "stub", "completed", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_when_gateway_fails_publishes_PaymentFailed_and_updates_row()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId);

        var gateway = new Mock<IPaymentGateway>();
        gateway
            .Setup(g => g.ProcessPaymentAsync(It.IsAny<ProcessPaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessPaymentGatewayResult.Failed("card_declined"));

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByOrderIdAsync(orderId, tenantId, "aeon", "stub", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(gateway.Object)
            .AddSingleton(repo.Object)
            .AddSingleton<IConfiguration>(TestConfiguration())
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(
                new ProcessPaymentV1(
                    Guid.NewGuid(),
                    orderId.ToString("D"),
                    tenantId.ToString("D"),
                    "cust",
                    10m,
                    "USD",
                    "card",
                    DateTimeOffset.UtcNow.AddMinutes(1)));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<PaymentFailedV1>());
            Assert.False(await harness.Published.Any<PaymentCompletedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(row.Id, tenantId, storeId, "aeon", "stub", "failed", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_when_row_already_succeeded_skips_gateway_and_republishes_PaymentCompleted()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId, PaymentTransactionStatus.Succeeded);

        var gateway = new Mock<IPaymentGateway>();
        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByOrderIdAsync(orderId, tenantId, "aeon", "stub", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(gateway.Object)
            .AddSingleton(repo.Object)
            .AddSingleton<IConfiguration>(TestConfiguration())
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(
                new ProcessPaymentV1(
                    Guid.NewGuid(),
                    orderId.ToString("D"),
                    tenantId.ToString("D"),
                    "cust",
                    10m,
                    "USD",
                    "card",
                    DateTimeOffset.UtcNow.AddMinutes(1)));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<PaymentCompletedV1>());
            gateway.Verify(
                g => g.ProcessPaymentAsync(It.IsAny<ProcessPaymentGatewayRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
            repo.Verify(r => r.UpdateStatusByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dcms:Client:Id"] = "aeon",
            })
            .Build();
}
