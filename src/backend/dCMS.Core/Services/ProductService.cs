using dCMS.Core.Commands;
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Core.Persistence;

namespace dCMS.Core.Services;

public sealed record GenerateVariantsResult(int CombinationCount, int Inserted, int SkippedDuplicates);

public sealed class ProductService(ICatalogPersistence persistence)
{
    private readonly ICatalogPersistence _persistence = persistence;

    public async Task<Product> CreateProductAsync(CreateProductCommand command, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = command.Slug.Trim().ToLowerInvariant();
        if (await _persistence.SlugExistsAsync(command.StoreId, normalizedSlug, cancellationToken).ConfigureAwait(false))
            throw new DuplicateProductSlugException(command.StoreId, normalizedSlug);

        var product = Product.Create(
            command.TenantId,
            command.StoreId,
            command.CategoryId,
            command.NameJson,
            command.DescriptionJson,
            command.Slug,
            now);

        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
        return product;
    }

    public async Task UpdateProductAsync(UpdateProductCommand command, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(command.ProductId, command.TenantId, command.StoreId, cancellationToken)
            .ConfigureAwait(false);

        var normalizedSlug = command.Slug.Trim().ToLowerInvariant();
        if (!string.Equals(product.Slug, normalizedSlug, StringComparison.Ordinal)
            && await _persistence.SlugExistsForAnotherProductAsync(product.StoreId, normalizedSlug, product.Id,
                cancellationToken).ConfigureAwait(false))
            throw new DuplicateProductSlugException(product.StoreId, normalizedSlug);

        product.UpdateDetails(command.CategoryId, command.NameJson, command.DescriptionJson, command.Slug, now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task PublishProductAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        product.Publish(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task ArchiveProductAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        product.Archive(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task SubmitForApprovalAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        product.SubmitForApproval(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task HideProductAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        product.Hide(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task UnhideProductAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        product.Unhide(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task<Product?> GetProductForStoreAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        var product = await _persistence.GetByIdAsync(productId, tenantId, cancellationToken).ConfigureAwait(false);
        if (product is null || product.StoreId != storeId)
            return null;
        return product;
    }

    public async Task<IReadOnlyList<ProductVariant>> ListVariantsAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        if (await GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            throw new ProductNotFoundException();

        return await _persistence.ListVariantsForProductAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateVariantAsync(string variantId, string productId, string tenantId, string storeId,
        string? sku, string? status, int? sortOrder, CancellationToken cancellationToken = default)
    {
        await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        var variants = await _persistence.ListVariantsForProductAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        var current = variants.FirstOrDefault(v => v.Id == variantId)
                      ?? throw new ProductNotFoundException();

        var newSku = string.IsNullOrWhiteSpace(sku) ? current.Sku : sku.Trim();
        if (newSku.Length > 256)
            throw new ArgumentException("SKU must be at most 256 characters.");

        var newStatus = string.IsNullOrWhiteSpace(status) ? current.Status : status.Trim().ToLowerInvariant();
        if (newStatus is not ("active" or "inactive"))
            throw new ArgumentException("Status must be 'active' or 'inactive'.");

        var newOrder = sortOrder ?? current.SortOrder;
        if (newOrder < 0)
            throw new ArgumentException("SortOrder must be non-negative.");

        if (!string.Equals(newSku, current.Sku, StringComparison.Ordinal)
            && await _persistence.VariantSkuTakenByAnotherAsync(storeId, newSku, variantId, cancellationToken)
                .ConfigureAwait(false))
            throw new DuplicateVariantSkuException(storeId, newSku);

        var updated = await _persistence.UpdateProductVariantAsync(variantId, productId, tenantId, storeId, newSku,
            newStatus, newOrder, cancellationToken).ConfigureAwait(false);
        if (updated == 0)
            throw new ProductNotFoundException();
    }

    public async Task<GenerateVariantsResult> GenerateVariantsAsync(GenerateVariantsCommand command, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(command.ProductId, command.TenantId, command.StoreId, cancellationToken)
            .ConfigureAwait(false);

        var combos = ProductVariantGeneratorService.GenerateCombinations(command.Axes);
        if (combos.Count == 0)
            return new GenerateVariantsResult(0, 0, 0);

        var existing = await _persistence.GetVariantCombinationHashesAsync(product.Id, cancellationToken)
            .ConfigureAwait(false);

        var prefix = string.IsNullOrWhiteSpace(command.SkuPrefix) ? "sku" : command.SkuPrefix.Trim();
        var nextOrder = await _persistence.GetMaxVariantSortOrderAsync(product.Id, cancellationToken)
            .ConfigureAwait(false) + 1;

        var newVariants = new List<ProductVariant>();
        var skipped = 0;
        foreach (var combo in combos)
        {
            var hash = ProductVariantGeneratorService.ComputeCombinationHash(combo);
            if (existing.Contains(hash))
            {
                skipped++;
                continue;
            }

            existing.Add(hash);
            var sku = BuildVariantSku(prefix, hash);
            var canonical = string.Join("|",
                combo.OrderBy(static kv => kv.Key).Select(static kv => $"{kv.Key}={kv.Value}"));
            newVariants.Add(ProductVariant.Create(product.Id, sku, hash, nextOrder, canonical));
            nextOrder++;
        }

        if (newVariants.Count == 0)
            return new GenerateVariantsResult(combos.Count, 0, skipped);

        product.RecordVariantsGenerated(now);
        await _persistence.SaveNewVariantsWithProductAsync(product, newVariants, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
        return new GenerateVariantsResult(combos.Count, newVariants.Count, skipped);
    }

    private static string BuildVariantSku(string prefix, string hash)
    {
        var safe = hash.Length >= 12 ? hash[..12] : hash;
        var sku = $"{prefix}-{safe}";
        return sku.Length <= 256 ? sku : sku[..256];
    }

    public Task<IReadOnlyList<CatalogCategoryRow>> ListCategoriesForTenantAsync(string tenantId,
        CancellationToken cancellationToken = default) =>
        _persistence.ListCategoriesByTenantAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<CatalogVariantAxisDefinition>> ListVariantAxesForStoreAsync(string tenantId,
        string storeId, CancellationToken cancellationToken = default) =>
        _persistence.ListVariantAxesForStoreAsync(tenantId, storeId, cancellationToken);

    private async Task<Product> LoadForStoreAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken)
    {
        var product = await _persistence.GetByIdAsync(productId, tenantId, cancellationToken).ConfigureAwait(false)
                      ?? throw new ProductNotFoundException();
        if (product.StoreId != storeId)
            throw new ProductNotFoundException();
        return product;
    }
}
