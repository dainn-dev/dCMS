using dCMS.Core.Messaging;
using dCMS.Inventory.Api.Messaging;
using dCMS.Inventory.Models;
using dCMS.Inventory.Persistence;
using dCMS.Inventory.Services;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace dCMS.Tests.Unit.Inventory;

public sealed class ReserveStockConsumerTests
{
    [Fact]
    public async Task Consume_success_publishes_StockReservedV1()
    {
        var stockRow = VariantStock.Restore(1, "var-1", "wh-1", quantity: 10, reservedQuantity: 0, rowVersion: 1);
        var persistence = new Mock<IInventoryStockPersistence>();
        persistence.Setup(p => p.GetStockAsync("t1", "s1", "var-1", "wh-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stockRow);
        persistence.Setup(p => p.CommitStockChangeAsync(
                "t1", "s1", It.IsAny<VariantStock>(), It.IsAny<StockMovement>(), It.IsAny<StockUpdatedV1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stockService = new StockService(persistence.Object);

        await using var provider = new ServiceCollection()
            .AddSingleton(stockService)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<ReserveStockConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid().ToString("D");
            await harness.Bus.Publish(new ReserveStockV1(
                Guid.NewGuid(),
                orderId,
                "t1",
                "s1",
                [new ReserveStockLineV1("var-1", "wh-1", 2)],
                DateTimeOffset.UtcNow.AddMinutes(30)));

            Assert.True(await harness.Consumed.Any<ReserveStockV1>());
            await harness.InactivityTask;
            Assert.True(await harness.Published.Any<StockReservedV1>(x => x.Context.Message.OrderId == orderId));
        }
        finally
        {
            await harness.Stop();
        }
    }
}
