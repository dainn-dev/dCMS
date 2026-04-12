using dCMS.Core.Commands;
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Core.Notifications;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using FluentAssertions;
using Moq;

namespace dCMS.Tests.Unit.Services;

public sealed class ProductServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-12T12:00:00Z");

    [Fact]
    public async Task CreateProduct_throws_when_slug_exists()
    {
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.SlugExistsAsync("store-a", "dup", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var act = async () => await svc.CreateProductAsync(
            new CreateProductCommand("t1", "store-a", 1, """{"vi":"X"}""", "{}", "dup"), Now);

        await act.Should().ThrowAsync<DuplicateProductSlugException>();
        persistence.Verify(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_persists_when_slug_free()
    {
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.SlugExistsAsync("store-a", "ok", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var p = await svc.CreateProductAsync(
            new CreateProductCommand("t1", "store-a", 1, """{"vi":"X"}""", "{}", "ok"), Now);

        p.StoreId.Should().Be("store-a");
        p.Slug.Should().Be("ok");
        persistence.Verify(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishProduct_throws_when_missing()
    {
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync("prod_x", "t1", It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var act = async () => await svc.PublishProductAsync("prod_x", "t1", "s1", Now);

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task PublishProduct_persists_when_found()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        product.ClearDomainEvents();

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        await svc.PublishProductAsync(product.Id, "t1", "s1", Now.AddMinutes(1));

        product.Status.Should().Be(ProductStatus.Active);
        persistence.Verify(x => x.SaveProductWithOutboxAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProduct_throws_when_slug_taken_by_other()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "mine", Now);
        product.ClearDomainEvents();

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.SlugExistsForAnotherProductAsync("s1", "taken", product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var act = async () => await svc.UpdateProductAsync(
            new UpdateProductCommand(product.Id, "t1", "s1", 2, """{"vi":"Y"}""", "{}", "taken"), Now.AddMinutes(1));

        await act.Should().ThrowAsync<DuplicateProductSlugException>();
        persistence.Verify(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProduct_persists_when_slug_free()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "mine", Now);
        product.ClearDomainEvents();

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.SlugExistsForAnotherProductAsync("s1", "other", product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        await svc.UpdateProductAsync(
            new UpdateProductCommand(product.Id, "t1", "s1", 2, """{"vi":"Y"}""", """{"en":"E"}""", "other"), Now.AddMinutes(1));

        product.Slug.Should().Be("other");
        product.CategoryId.Should().Be(2);
        persistence.Verify(x => x.SaveProductWithOutboxAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitForApproval_persists()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        persistence.Setup(x => x.InsertApprovalCommentAsync(product.Id, "user-1", "store_manager", "Submitted for approval.", "submitted",
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        await svc.SubmitForApprovalAsync(product.Id, "t1", "s1", "user-1", "store_manager", Now.AddMinutes(1));

        product.Status.Should().Be(ProductStatus.PendingApproval);
        persistence.Verify(x => x.SaveProductWithOutboxAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovePendingProduct_sets_active_and_inserts_comment()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        product.SubmitForApproval(Now.AddMinutes(1));

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        persistence.Setup(x => x.InsertApprovalCommentAsync(product.Id, "u1", "ChainAdmin", "Approved.", "approve", It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        await svc.ApprovePendingProductAsync(product.Id, "t1", "s1", "u1", "ChainAdmin", Now.AddMinutes(2));

        product.Status.Should().Be(ProductStatus.Active);
        persistence.Verify(
            x => x.InsertApprovalCommentAsync(product.Id, "u1", "ChainAdmin", "Approved.", "approve", It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestChangesOnPending_returns_draft_and_inserts_comment()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        product.SubmitForApproval(Now.AddMinutes(1));

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        persistence.Setup(x => x.InsertApprovalCommentAsync(product.Id, "u1", "ChainAdmin", "Fix slug", "request_change", It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        await svc.RequestChangesOnPendingProductAsync(product.Id, "t1", "s1", "Fix slug", "u1", "ChainAdmin", Now.AddMinutes(2));

        product.Status.Should().Be(ProductStatus.Draft);
        persistence.Verify(
            x => x.InsertApprovalCommentAsync(product.Id, "u1", "ChainAdmin", "Fix slug", "request_change", It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductForStore_returns_null_when_store_mismatch()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var result = await svc.GetProductForStoreAsync(product.Id, "t1", "other", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task PublishProduct_throws_when_store_mismatch()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var act = async () => await svc.PublishProductAsync(product.Id, "t1", "other-store", Now);

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task UpdateVariantAsync_calls_persistence_when_sku_free()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var variants = new[]
        {
            ProductVariant.Restore("v1", product.Id, "old-sku", "hashaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "active", 0)
        };
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.ListVariantsForProductAsync(product.Id, "t1", "s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(variants);
        persistence.Setup(x => x.VariantSkuTakenByAnotherAsync("s1", "new-sku", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        persistence.Setup(x => x.UpdateProductVariantAsync("v1", product.Id, "t1", "s1", "new-sku", "inactive", 3, 0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        persistence.Setup(x => x.SaveProductWithOutboxAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        await svc.UpdateVariantAsync("v1", product.Id, "t1", "s1", "new-sku", "inactive", 3, null, Now);

        persistence.Verify(
            x => x.UpdateProductVariantAsync("v1", product.Id, "t1", "s1", "new-sku", "inactive", 3, 0,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateVariantAsync_throws_DuplicateVariantSku_when_sku_taken()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var variants = new[]
        {
            ProductVariant.Restore("v1", product.Id, "sku-a", "hashaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "active", 0)
        };
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.ListVariantsForProductAsync(product.Id, "t1", "s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(variants);
        persistence.Setup(x => x.VariantSkuTakenByAnotherAsync("s1", "taken", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var act = async () => await svc.UpdateVariantAsync("v1", product.Id, "t1", "s1", "taken", null, null, null, Now);

        await act.Should().ThrowAsync<DuplicateVariantSkuException>();
        persistence.Verify(
            x => x.UpdateProductVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateManualVariantAsync_throws_DuplicateVariantCombinationHash_when_hash_exists()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var hash = new string('a', 64);
        var existing = ProductVariant.Restore("v-old", product.Id, "sku-x", hash, "active", 0);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.VariantSkuTakenByAnotherAsync("s1", "new-sku", "__dcms_new__", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        persistence.Setup(x => x.ListVariantsForProductAsync(product.Id, "t1", "s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var act = async () =>
            await svc.CreateManualVariantAsync(product.Id, "t1", "s1", "new-sku", hash, "", 0, "active", Now);

        (await act.Should().ThrowAsync<DuplicateVariantCombinationHashException>()).Which.ConflictingVariantId.Should()
            .Be("v-old");
        persistence.Verify(x => x.SaveNewVariantsWithProductAsync(It.IsAny<Product>(), It.IsAny<IReadOnlyList<ProductVariant>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualVariantAsync_inserts_and_outboxes()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "slug", Now);
        var hash = new string('c', 64);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.VariantSkuTakenByAnotherAsync("s1", "manual-sku", "__dcms_new__", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        persistence.Setup(x => x.ListVariantsForProductAsync(product.Id, "t1", "s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProductVariant>());
        persistence.Setup(x => x.GetMaxVariantSortOrderAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        persistence.Setup(x => x.SaveNewVariantsWithProductAsync(It.IsAny<Product>(), It.IsAny<IReadOnlyList<ProductVariant>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new ProductService(persistence.Object, NullProductNotificationSink.Instance);

        var v = await svc.CreateManualVariantAsync(product.Id, "t1", "s1", "manual-sku", hash, "1=2|3=4", 99_000, "inactive", Now);

        v.Sku.Should().Be("manual-sku");
        v.CombinationHash.Should().Be(hash);
        v.CombinationCanonical.Should().Be("1=2|3=4");
        v.BasePriceAmount.Should().Be(99_000);
        v.Status.Should().Be("inactive");
        persistence.Verify(x => x.SaveNewVariantsWithProductAsync(product, It.Is<IReadOnlyList<ProductVariant>>(l => l.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

