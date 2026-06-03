using dCMS.Core.Commands;
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Core.Notifications;
using dCMS.Core.Persistence;

namespace dCMS.Core.Services;

public sealed record GenerateVariantsResult(int CombinationCount, int Inserted, int SkippedDuplicates);

public sealed class ProductService(ICatalogPersistence persistence, IProductNotificationSink notificationSink)
{
    private readonly ICatalogPersistence _persistence = persistence;
    private readonly IProductNotificationSink _notificationSink = notificationSink;

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
            now,
            command.BrandId);

        if (command.Metadata is not null)
            product.UpdatePageMetadata(command.Metadata, now);

        if (command.CustomFieldsJson is not null)
            product.UpdateCustomFields(command.CustomFieldsJson, now);

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

        product.UpdateDetails(command.CategoryId, command.NameJson, command.DescriptionJson, command.Slug, now,
            command.BrandId);
        if (command.Metadata is not null)
            product.UpdatePageMetadata(command.Metadata, now);
        if (command.CustomFieldsJson is not null)
            product.UpdateCustomFields(command.CustomFieldsJson, now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    /// <summary>Ordered manual recommendations (related products) for a product.</summary>
    public Task<IReadOnlyList<ProductRecommendationRow>> ListRecommendationsAsync(string productId, string tenantId,
        string storeId, CancellationToken cancellationToken = default) =>
        _persistence.ListRecommendationsForProductAsync(productId, tenantId, storeId, cancellationToken);

    /// <summary>Replaces the manual recommendation list for a product; returns the persisted ordered ids.</summary>
    public Task<IReadOnlyList<string>> SetRecommendationsAsync(string productId, string tenantId, string storeId,
        IReadOnlyList<string> recommendedProductIds, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        _persistence.SetRecommendationsForProductAsync(productId, tenantId, storeId, recommendedProductIds, now,
            cancellationToken);

    public async Task PublishProductAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        product.Publish(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        await _persistence.InsertApprovalCommentAsync(product.Id, "system", "store_manager", "Published.", "approve", now,
            cancellationToken).ConfigureAwait(false);
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

    public async Task SubmitForApprovalAsync(string productId, string tenantId, string storeId, string actorUserId, string actorRole,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        product.SubmitForApproval(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        await _persistence.InsertApprovalCommentAsync(product.Id, actorUserId, actorRole, "Submitted for approval.",
            "submitted", now, cancellationToken).ConfigureAwait(false);
        await _notificationSink.OnProductSubmittedAsync(tenantId, storeId, productId, actorUserId, cancellationToken)
            .ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    /// <summary>active|hidden → pending_archive; records an "archive_request" approval comment for the queue.</summary>
    public async Task SubmitForArchiveAsync(string productId, string tenantId, string storeId, string actorUserId,
        string actorRole, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        product.SubmitForArchive(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        await _persistence.InsertApprovalCommentAsync(product.Id, actorUserId, actorRole, "Submitted for archive.",
            "archive_request", now, cancellationToken).ConfigureAwait(false);
        await _notificationSink.OnProductSubmittedAsync(tenantId, storeId, productId, actorUserId, cancellationToken)
            .ConfigureAwait(false);
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

    public async Task<IReadOnlyList<ApprovalCommentRow>> ListApprovalCommentsAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        if (await GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            throw new ProductNotFoundException();

        return await _persistence.ListApprovalCommentsForProductAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddApprovalCommentAsync(string productId, string tenantId, string storeId, string message, string userId,
        string role, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.");

        var trimmed = message.Trim();
        if (trimmed.Length > 8000)
            throw new ArgumentException("Message must be at most 8000 characters.");

        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (product.Status != ProductStatus.PendingApproval)
            throw new InvalidProductStateException("Comments can only be added while the product is pending approval.");

        await _persistence.InsertApprovalCommentAsync(product.Id, userId, role, trimmed, "comment", now, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> GetStoreApprovalRequiredAsync(string tenantId, string storeId, CancellationToken cancellationToken = default)
    {
        var row = await _persistence.GetStoreCatalogSettingsAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        return row?.ApprovalRequired ?? false;
    }

    public async Task<ProductStatus> ApprovePendingProductAsync(string productId, string tenantId, string storeId, string userId,
        string role, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        if (product.Status == ProductStatus.PendingArchive)
        {
            product.ApproveArchive(now);
            await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
            await _persistence.InsertApprovalCommentAsync(product.Id, userId, role, "Archive approved.", "approve_archive",
                now, cancellationToken).ConfigureAwait(false);
            await _notificationSink.OnProductApprovedAsync(tenantId, storeId, product.Id, cancellationToken).ConfigureAwait(false);
            product.ClearDomainEvents();
            return product.Status;
        }

        if (product.Status != ProductStatus.PendingApproval)
            throw new InvalidProductStateException("Product is not pending approval.");

        product.Publish(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        await _persistence.InsertApprovalCommentAsync(product.Id, userId, role, "Approved.", "approve", now, cancellationToken)
            .ConfigureAwait(false);
        await _notificationSink.OnProductApprovedAsync(tenantId, storeId, product.Id, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
        return product.Status;
    }

    public async Task RequestChangesOnPendingProductAsync(string productId, string tenantId, string storeId, string message,
        string userId, string role, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Comment is required.");

        var trimmed = message.Trim();
        if (trimmed.Length > 8000)
            throw new ArgumentException("Comment must be at most 8000 characters.");

        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (product.Status != ProductStatus.PendingApproval)
            throw new InvalidProductStateException("Product is not pending approval.");

        product.ReturnPendingToDraft(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        await _persistence.InsertApprovalCommentAsync(product.Id, userId, role, trimmed, "request_change", now, cancellationToken)
            .ConfigureAwait(false);
        await _notificationSink.OnProductRequestChangesAsync(tenantId, storeId, product.Id, trimmed, cancellationToken)
            .ConfigureAwait(false);
        product.ClearDomainEvents();
    }

    public async Task RejectPendingProductAsync(string productId, string tenantId, string storeId, string message, string userId,
        string role, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Comment is required.");

        var trimmed = message.Trim();
        if (trimmed.Length > 8000)
            throw new ArgumentException("Comment must be at most 8000 characters.");

        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        if (product.Status == ProductStatus.PendingArchive)
        {
            product.CancelArchiveRequest(now);
            await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
            await _persistence.InsertApprovalCommentAsync(product.Id, userId, role, trimmed, "reject_archive", now,
                cancellationToken).ConfigureAwait(false);
            await _notificationSink.OnProductRejectedAsync(tenantId, storeId, product.Id, trimmed, cancellationToken)
                .ConfigureAwait(false);
            product.ClearDomainEvents();
            return;
        }

        if (product.Status != ProductStatus.PendingApproval)
            throw new InvalidProductStateException("Product is not pending approval.");

        product.ReturnPendingToDraft(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        await _persistence.InsertApprovalCommentAsync(product.Id, userId, role, trimmed, "reject", now, cancellationToken)
            .ConfigureAwait(false);
        await _notificationSink.OnProductRejectedAsync(tenantId, storeId, product.Id, trimmed, cancellationToken)
            .ConfigureAwait(false);
        product.ClearDomainEvents();
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
        string? sku, string? status, int? sortOrder, long? basePriceAmount, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

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

        var newPrice = basePriceAmount ?? current.BasePriceAmount;
        if (newPrice < 0)
            throw new ArgumentException("BasePriceAmount must be non-negative.");

        if (!string.Equals(newSku, current.Sku, StringComparison.Ordinal)
            && await _persistence.VariantSkuTakenByAnotherAsync(storeId, newSku, variantId, cancellationToken)
                .ConfigureAwait(false))
            throw new DuplicateVariantSkuException(storeId, newSku);

        var updated = await _persistence.UpdateProductVariantAsync(variantId, productId, tenantId, storeId, newSku,
            newStatus, newOrder, newPrice, cancellationToken).ConfigureAwait(false);
        if (updated == 0)
            throw new ProductNotFoundException();

        if (newSku != current.Sku || newStatus != current.Status || newOrder != current.SortOrder ||
            newPrice != current.BasePriceAmount)
        {
            product.RecordVariantRowsUpdated(now);
            await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
            product.ClearDomainEvents();
        }
    }

    public async Task<(IReadOnlyList<object> Succeeded, IReadOnlyList<object> Failed)> BulkUpdateVariantPricesAsync(
        string tenantId, string storeId, IReadOnlyList<(string ProductId, string VariantId, long BasePriceAmount)> items,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<object>();
        var failed = new List<object>();
        for (var i = 0; i < items.Count; i++)
        {
            var (productId, variantId, price) = items[i];
            try
            {
                if (price < 0)
                    throw new ArgumentException("BasePriceAmount must be non-negative.");

                var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken)
                    .ConfigureAwait(false);
                var variants = await _persistence
                    .ListVariantsForProductAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
                var current = variants.FirstOrDefault(v => v.Id == variantId)
                              ?? throw new ProductNotFoundException();

                var updated = await _persistence.UpdateProductVariantAsync(variantId, productId, tenantId, storeId,
                    current.Sku, current.Status, current.SortOrder, price, cancellationToken).ConfigureAwait(false);
                if (updated == 0)
                    throw new ProductNotFoundException();

                product.RecordVariantRowsUpdated(now);
                await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
                product.ClearDomainEvents();
                succeeded.Add(new { index = i, productId, variantId });
            }
            catch (Exception ex) when (ex is ArgumentException or ProductNotFoundException)
            {
                failed.Add(new { index = i, productId, variantId, message = ex.Message });
            }
        }

        return (succeeded, failed);
    }

    public async Task TouchProductAsync(string productId, string tenantId, string storeId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        product.RecordVariantRowsUpdated(now);
        await _persistence.SaveProductWithOutboxAsync(product, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
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

    /// <summary>DAI-288: add one SKU row with explicit combination hash (post wizard / backoffice).</summary>
    public async Task<ProductVariant> CreateManualVariantAsync(string productId, string tenantId, string storeId,
        string sku, string combinationHash, string? combinationCanonical, long basePriceAmount, string status,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var product = await LoadForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);

        if (product.Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot add variants to an archived product.");

        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.");
        var skuNorm = sku.Trim();
        if (skuNorm.Length > 256)
            throw new ArgumentException("SKU must be at most 256 characters.");

        if (string.IsNullOrWhiteSpace(combinationHash))
            throw new ArgumentException("combinationHash is required.");
        var hash = combinationHash.Trim().ToLowerInvariant();
        if (hash.Length != 64)
            throw new ArgumentException("combinationHash must be a 64-character lowercase hex SHA-256 string.");
        foreach (var c in hash)
        {
            if (c is < '0' or > '9' and < 'a' or > 'f')
                throw new ArgumentException("combinationHash must be hexadecimal (a-f, 0-9).");
        }

        var canon = string.IsNullOrWhiteSpace(combinationCanonical) ? "" : combinationCanonical.Trim();
        if (canon.Length > 256)
            throw new ArgumentException("combinationCanonical must be at most 256 characters.");

        if (basePriceAmount < 0)
            throw new ArgumentException("BasePriceAmount must be non-negative.");

        var st = string.IsNullOrWhiteSpace(status) ? "active" : status.Trim().ToLowerInvariant();
        if (st is not ("active" or "inactive"))
            throw new ArgumentException("Status must be 'active' or 'inactive'.");

        if (await _persistence.VariantSkuTakenByAnotherAsync(storeId, skuNorm, "__dcms_new__", cancellationToken)
                .ConfigureAwait(false))
            throw new DuplicateVariantSkuException(storeId, skuNorm);

        var variants = await _persistence.ListVariantsForProductAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        var dup = variants.FirstOrDefault(v => string.Equals(v.CombinationHash, hash, StringComparison.Ordinal));
        if (dup is not null)
            throw new DuplicateVariantCombinationHashException(dup.Id, hash);

        var nextOrder = await _persistence.GetMaxVariantSortOrderAsync(product.Id, cancellationToken)
            .ConfigureAwait(false) + 1;

        var row = ProductVariant.Create(product.Id, skuNorm, hash, nextOrder, canon, basePriceAmount);
        if (st == "inactive")
            row = row.With(status: "inactive");

        product.RecordVariantsGenerated(now);
        await _persistence.SaveNewVariantsWithProductAsync(product, new[] { row }, cancellationToken).ConfigureAwait(false);
        product.ClearDomainEvents();
        return row;
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
