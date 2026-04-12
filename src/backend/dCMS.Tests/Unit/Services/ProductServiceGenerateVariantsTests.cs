using dCMS.Core.Commands;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using FluentAssertions;
using Moq;
using Axis = dCMS.Core.Services.ProductVariantGeneratorService.VariantAxisDefinition;

namespace dCMS.Tests.Unit.Services;

public sealed class ProductServiceGenerateVariantsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-12T14:00:00Z");

    [Fact]
    public async Task GenerateVariants_inserts_new_and_skips_existing_hashes()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "p", Now);
        var hashForFirst = ProductVariantGeneratorService.ComputeCombinationHash(new Dictionary<int, int> { [2] = 5 });

        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.GetVariantCombinationHashesAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { hashForFirst });
        persistence.Setup(x => x.GetMaxVariantSortOrderAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(9);
        persistence.Setup(x => x.SaveNewVariantsWithProductAsync(It.IsAny<Product>(), It.IsAny<IReadOnlyList<ProductVariant>>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var svc = new ProductService(persistence.Object);
        var axes = new[]
        {
            new Axis(2, new[] { 5, 6 })
        };

        var result = await svc.GenerateVariantsAsync(
            new GenerateVariantsCommand(product.Id, "t1", "s1", axes, "sku"), Now.AddMinutes(1));

        result.CombinationCount.Should().Be(2);
        result.Inserted.Should().Be(1);
        result.SkippedDuplicates.Should().Be(1);

        persistence.Verify(
            x => x.SaveNewVariantsWithProductAsync(product, It.Is<IReadOnlyList<ProductVariant>>(l => l.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateVariants_returns_zeros_when_no_axes()
    {
        var product = Product.Create("t1", "s1", 1, """{"vi":"X"}""", "{}", "p", Now);
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.GetByIdAsync(product.Id, "t1", It.IsAny<CancellationToken>())).ReturnsAsync(product);
        persistence.Setup(x => x.GetVariantCombinationHashesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        var svc = new ProductService(persistence.Object);

        var result = await svc.GenerateVariantsAsync(
            new GenerateVariantsCommand(product.Id, "t1", "s1", Array.Empty<Axis>(), "sku"), Now);

        result.Should().Be(new GenerateVariantsResult(0, 0, 0));
        persistence.Verify(x => x.SaveNewVariantsWithProductAsync(It.IsAny<Product>(), It.IsAny<IReadOnlyList<ProductVariant>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
