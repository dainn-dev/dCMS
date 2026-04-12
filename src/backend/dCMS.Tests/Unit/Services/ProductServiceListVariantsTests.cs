using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Core.Notifications;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using FluentAssertions;
using Moq;

namespace dCMS.Tests.Unit.Services;

public sealed class ProductServiceListVariantsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-12T14:00:00Z");

    [Fact]
    public async Task ListVariants_returns_rows_from_persistence()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "p", Now);
        var v1 = ProductVariant.Restore("var_1", product.Id, "sku-abc", "hash1", "active", 0);
        var v2 = ProductVariant.Restore("var_2", product.Id, "sku-def", "hash2", "active", 1);

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.ListVariantsForProductAsync(product.Id, "t1", "s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { v1, v2 });

        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);
        var list = await svc.ListVariantsAsync(product.Id, "t1", "s1");

        list.Should().HaveCount(2).And.Subject.Should().ContainInOrder(v1, v2);
        persistence.Verify(x => x.ListVariantsForProductAsync(product.Id, "t1", "s1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListVariants_throws_when_product_not_in_store()
    {
        var product = Product.Create("t1", "other-store", 1, """{"vi":"X"}""", "{}", "p", Now);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);
        await Assert.ThrowsAsync<ProductNotFoundException>(() => svc.ListVariantsAsync(product.Id, "t1", "s1"));
        persistence.Verify(x => x.ListVariantsForProductAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListVariants_throws_when_product_missing()
    {
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync("missing", "t1", It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);
        await Assert.ThrowsAsync<ProductNotFoundException>(() => svc.ListVariantsAsync("missing", "t1", "s1"));
    }
}
