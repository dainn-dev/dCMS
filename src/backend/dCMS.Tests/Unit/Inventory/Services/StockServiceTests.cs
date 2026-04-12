using dCMS.Core.Exceptions;
using dCMS.Inventory.Commands;
using dCMS.Inventory.Models;
using dCMS.Inventory.Persistence;
using dCMS.Inventory.Services;
using FluentAssertions;
using Moq;

namespace dCMS.Tests.Unit.Inventory.Services;

public sealed class StockServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-12T15:00:00Z");

    [Fact]
    public async Task ReserveStock_retries_on_concurrency_then_succeeds()
    {
        var stock1 = VariantStock.Restore(1, "var_1", "wh_1", 10, 0, 1L);
        var stock2 = VariantStock.Restore(1, "var_1", "wh_1", 10, 0, 2L);

        var persistence = new Mock<IInventoryStockPersistence>();
        var queue = new Queue<VariantStock>(new[] { stock1, stock2 });
        persistence.Setup(x => x.GetStockAsync("t1", "s1", "var_1", "wh_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Dequeue());

        var calls = 0;
        persistence.Setup(x => x.CommitStockChangeAsync("t1", "s1", It.IsAny<VariantStock>(), It.IsAny<StockMovement>(),
                It.IsAny<dCMS.Core.Messaging.StockUpdatedV1>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                    throw new StockConcurrencyException("var_1", "wh_1");
                return Task.CompletedTask;
            });

        var sut = new StockService(persistence.Object);
        await sut.ReserveStockAsync(
            new ReserveStockCommand("t1", "s1", "var_1", "wh_1", 2, "test", null), Now, CancellationToken.None);

        calls.Should().Be(2);
        persistence.Verify(x => x.GetStockAsync("t1", "s1", "var_1", "wh_1", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReserveStock_throws_after_max_retries()
    {
        var persistence = new Mock<IInventoryStockPersistence>();
        persistence.Setup(x => x.GetStockAsync("t1", "s1", "var_1", "wh_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => VariantStock.Restore(1, "var_1", "wh_1", 10, 0, 1L));
        persistence.Setup(x => x.CommitStockChangeAsync("t1", "s1", It.IsAny<VariantStock>(), It.IsAny<StockMovement>(),
                It.IsAny<dCMS.Core.Messaging.StockUpdatedV1>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StockConcurrencyException("var_1", "wh_1"));

        var sut = new StockService(persistence.Object);
        await Assert.ThrowsAsync<StockConcurrencyException>(() =>
            sut.ReserveStockAsync(new ReserveStockCommand("t1", "s1", "var_1", "wh_1", 1, "test", null), Now,
                CancellationToken.None));

        persistence.Verify(x => x.GetStockAsync("t1", "s1", "var_1", "wh_1", It.IsAny<CancellationToken>()), Times.Exactly(4));
        persistence.Verify(x => x.CommitStockChangeAsync("t1", "s1", It.IsAny<VariantStock>(), It.IsAny<StockMovement>(),
            It.IsAny<dCMS.Core.Messaging.StockUpdatedV1>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task ReserveStock_throws_OutOfStock_without_retry_or_commit()
    {
        var stock = VariantStock.Restore(1, "var_1", "wh_1", 5, 4, 1L);
        var persistence = new Mock<IInventoryStockPersistence>();
        persistence.Setup(x => x.GetStockAsync("t1", "s1", "var_1", "wh_1", It.IsAny<CancellationToken>())).ReturnsAsync(stock);

        var sut = new StockService(persistence.Object);
        await Assert.ThrowsAsync<OutOfStockException>(() =>
            sut.ReserveStockAsync(new ReserveStockCommand("t1", "s1", "var_1", "wh_1", 5, "test", null), Now,
                CancellationToken.None));

        persistence.Verify(x => x.CommitStockChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VariantStock>(),
            It.IsAny<StockMovement>(), It.IsAny<dCMS.Core.Messaging.StockUpdatedV1>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AdjustStock_throws_StockInvariant_without_commit()
    {
        var stock = VariantStock.Restore(1, "var_1", "wh_1", 50, 40, 1L);
        var persistence = new Mock<IInventoryStockPersistence>();
        persistence.Setup(x => x.GetStockAsync("t1", "s1", "var_1", "wh_1", It.IsAny<CancellationToken>())).ReturnsAsync(stock);

        var sut = new StockService(persistence.Object);
        await Assert.ThrowsAsync<StockInvariantException>(() =>
            sut.AdjustStockAsync(new AdjustStockCommand("t1", "s1", "var_1", "wh_1", -20, "test", null), Now,
                CancellationToken.None));

        persistence.Verify(x => x.CommitStockChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VariantStock>(),
            It.IsAny<StockMovement>(), It.IsAny<dCMS.Core.Messaging.StockUpdatedV1>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
