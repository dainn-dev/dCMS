using dCMS.Core.Messaging;
using dCMS.Payment.Core;
using dCMS.Payment.Infrastructure.Persistence;
using dCMS.Payment.Infrastructure.Webhooks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace dCMS.Payment.Tests.Webhooks;

public sealed class PaymentGatewayWebhookProcessorTests
{
    private static PaymentTransaction Row(
        Guid orderId,
        Guid tenantId,
        Guid storeId,
        PaymentTransactionStatus status = PaymentTransactionStatus.Initiated,
        string intentId = "pi_wh_1") =>
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
    public async Task ProcessAsync_when_initiated_and_succeeds_updates_and_publishes_PaymentCompleted()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId);

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByPaymentIntentIdAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(repo.Object)
            .AddMassTransitTestHarness(_ => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var sut = new PaymentGatewayWebhookProcessor(
                repo.Object,
                harness.Bus,
                NullLogger<PaymentGatewayWebhookProcessor>.Instance);

            var outcome = await sut.ProcessAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, succeeded: true, "ch_wh", "", CancellationToken.None);

            Assert.Equal(PaymentWebhookProcessResult.Ok, outcome);
            await harness.InactivityTask;
            Assert.True(await harness.Published.Any<PaymentCompletedV1>());
            Assert.False(await harness.Published.Any<PaymentFailedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, "aeon", row.Provider, "completed", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ProcessAsync_when_already_succeeds_does_not_republish()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId, PaymentTransactionStatus.Succeeded);

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByPaymentIntentIdAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(repo.Object)
            .AddMassTransitTestHarness(_ => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var sut = new PaymentGatewayWebhookProcessor(
                repo.Object,
                harness.Bus,
                NullLogger<PaymentGatewayWebhookProcessor>.Instance);

            var outcome = await sut.ProcessAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, succeeded: true, null, "", CancellationToken.None);

            Assert.Equal(PaymentWebhookProcessResult.OkAlreadyProcessed, outcome);
            await harness.InactivityTask;
            Assert.False(await harness.Published.Any<PaymentCompletedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ProcessAsync_when_initiated_and_fails_updates_and_publishes_PaymentFailed()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId);

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByPaymentIntentIdAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await using var provider = new ServiceCollection()
            .AddSingleton(repo.Object)
            .AddMassTransitTestHarness(_ => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var sut = new PaymentGatewayWebhookProcessor(
                repo.Object,
                harness.Bus,
                NullLogger<PaymentGatewayWebhookProcessor>.Instance);

            var outcome = await sut.ProcessAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, succeeded: false, null, "card_declined", CancellationToken.None);

            Assert.Equal(PaymentWebhookProcessResult.Ok, outcome);
            await harness.InactivityTask;
            Assert.True(await harness.Published.Any<PaymentFailedV1>());
            Assert.False(await harness.Published.Any<PaymentCompletedV1>());
            repo.Verify(r => r.UpdateStatusByIdAsync(row.Id, row.TenantId, row.StoreId, "aeon", row.Provider, "failed", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ProcessAsync_when_succeeded_row_gets_failure_event_returns_Conflict()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId, PaymentTransactionStatus.Succeeded);

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByPaymentIntentIdAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        var sut = new PaymentGatewayWebhookProcessor(
            repo.Object,
            Mock.Of<IBus>(),
            NullLogger<PaymentGatewayWebhookProcessor>.Instance);

        var outcome = await sut.ProcessAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, succeeded: false, null, "card_declined", CancellationToken.None);

        Assert.Equal(PaymentWebhookProcessResult.Conflict, outcome);
    }

    [Fact]
    public async Task ProcessAsync_when_failed_row_gets_success_event_returns_Conflict()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var row = Row(orderId, tenantId, storeId, PaymentTransactionStatus.Failed);

        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByPaymentIntentIdAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        var sut = new PaymentGatewayWebhookProcessor(
            repo.Object,
            Mock.Of<IBus>(),
            NullLogger<PaymentGatewayWebhookProcessor>.Instance);

        var outcome = await sut.ProcessAsync(row.PaymentIntentId, row.TenantId, "aeon", row.Provider, succeeded: true, "ch_x", "", CancellationToken.None);

        Assert.Equal(PaymentWebhookProcessResult.Conflict, outcome);
    }

    [Fact]
    public async Task ProcessAsync_when_unknown_intent_returns_UnknownIntent()
    {
        var repo = new Mock<IPaymentTransactionRepository>();
        repo.Setup(r => r.GetLatestByPaymentIntentIdAsync("pi_missing", It.IsAny<Guid>(), "aeon", "stub", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        await using var provider = new ServiceCollection()
            .AddSingleton(repo.Object)
            .AddMassTransitTestHarness(_ => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var sut = new PaymentGatewayWebhookProcessor(
                repo.Object,
                harness.Bus,
                NullLogger<PaymentGatewayWebhookProcessor>.Instance);

            var outcome = await sut.ProcessAsync("pi_missing", Guid.NewGuid(), "aeon", "stub", succeeded: true, null, "", CancellationToken.None);

            Assert.Equal(PaymentWebhookProcessResult.UnknownIntent, outcome);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
