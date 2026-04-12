using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>Transactional catalog persistence (product row + outbox in one DB transaction).</summary>
public interface ICatalogPersistence
{
    Task<Product?> GetByIdAsync(string productId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Lookup by store + tenant + slug (any status). Caller filters for public storefront.</summary>
    Task<Product?> GetBySlugAsync(string storeId, string tenantId, string slug,
        CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string storeId, string slug, CancellationToken cancellationToken = default);

    /// <summary>True if another product in the same store already uses the slug (excludes <paramref name="excludeProductId"/>).</summary>
    Task<bool> SlugExistsForAnotherProductAsync(string storeId, string slug, string excludeProductId,
        CancellationToken cancellationToken = default);

    Task SaveProductWithOutboxAsync(Product product, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetVariantCombinationHashesAsync(string productId,
        CancellationToken cancellationToken = default);

    Task<int> GetMaxVariantSortOrderAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>Single transaction: multi-row insert variants + update product + outbox for product domain events.</summary>
    Task SaveNewVariantsWithProductAsync(Product product, IReadOnlyList<ProductVariant> newVariants,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductVariant>> ListVariantsForProductAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolve SPU id for a SKU row (e.g. StockSync / ES reindex).</summary>
    Task<string?> GetProductIdByVariantIdAsync(string variantId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    /// <summary>True if another variant in the same store already uses <paramref name="sku"/>.</summary>
    Task<bool> VariantSkuTakenByAnotherAsync(string storeId, string sku, string excludeVariantId,
        CancellationToken cancellationToken = default);

    /// <summary>Updates variant row when it belongs to <paramref name="productId"/> in the tenant/store. Returns rows affected (0 if not found).</summary>
    Task<int> UpdateProductVariantAsync(string variantId, string productId, string tenantId, string storeId,
        string sku, string status, int sortOrder, long basePriceAmount, CancellationToken cancellationToken = default);

    /// <summary>All categories for a tenant (flat list; client builds tree). US-13 step 1.</summary>
    Task<IReadOnlyList<CatalogCategoryRow>> ListCategoriesByTenantAsync(string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Variant axes for a store: tenant attributes + values, filtered by <c>StoreCatalogAttributeValues</c> when the store has any allowlist rows (DAI-284).
    /// </summary>
    Task<IReadOnlyList<CatalogVariantAxisDefinition>> ListVariantAxesForStoreAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalCommentRow>> ListApprovalCommentsForProductAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task InsertApprovalCommentAsync(string productId, string userId, string role, string message, string type,
        DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    Task<int> CountUnreadNotificationsAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationEventRow>> ListNotificationsForUserAsync(string tenantId, string userId, int limit,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllNotificationsReadAsync(string tenantId, string userId, DateTimeOffset readAt,
        CancellationToken cancellationToken = default);

    Task InsertNotificationAsync(string tenantId, string userId, string type, string entityId, string message,
        DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    Task<StoreCatalogSettingsRow?> GetStoreCatalogSettingsAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task UpsertStoreCatalogSettingsAsync(string tenantId, string storeId, bool approvalRequired, int? lowStockThreshold,
        DateTimeOffset updatedAt, CancellationToken cancellationToken = default);

    /// <summary>Latest <c>ApprovalComments.UserId</c> for product/type (e.g. <c>submitted</c> submitter).</summary>
    Task<string?> GetLatestApprovalCommentUserIdAsync(string productId, string tenantId, string storeId, string type,
        CancellationToken cancellationToken = default);
}
