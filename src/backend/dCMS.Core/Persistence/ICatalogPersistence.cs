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

    // ── Category write methods (DAI-586) ────────────────────────────────────

    Task<CatalogCategoryRow?> GetCategoryByIdAsync(int id, string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>True if a category in this tenant already uses <paramref name="slug"/> (excludes <paramref name="excludeId"/>).</summary>
    Task<bool> CategorySlugExistsAsync(string tenantId, string slug, int? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>Insert category row and return new auto-generated Id.</summary>
    Task<int> CreateCategoryAsync(CatalogCategoryRow row, CancellationToken cancellationToken = default);

    /// <summary>Update all fields except Id, TenantId, Path, Depth (those are managed by reclassify). Returns false if not found.</summary>
    Task<bool> UpdateCategoryAsync(CatalogCategoryRow row, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete category and all descendants. Returns false if not found.</summary>
    Task<bool> DeleteCategoryAsync(int id, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Move category to a new parent, recomputing Path + Depth for node and all descendants.</summary>
    Task<bool> ReclassifyCategoryAsync(int id, string tenantId, int? newParentId,
        CancellationToken cancellationToken = default);

    /// <summary>Reorder siblings: update SortOrder for each item in <paramref name="order"/>.</summary>
    Task ReorderSiblingsAsync(string tenantId, int? parentId,
        IReadOnlyList<(int Id, int SortOrder)> order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Variant axes for a store: tenant attributes + values, filtered by <c>StoreCatalogAttributeValues</c> when the store has any allowlist rows (DAI-284).
    /// </summary>
    Task<IReadOnlyList<CatalogVariantAxisDefinition>> ListVariantAxesForStoreAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    // ── Attribute management methods (DAI-592) ────────────────────────────────

    Task<IReadOnlyList<CatalogAttributeRow>> ListAttributesAsync(string tenantId, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAttributesAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<CatalogAttributeRow?> GetAttributeByIdAsync(int id, string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>True if another attribute in this tenant already uses <paramref name="code"/> (excludes <paramref name="excludeId"/>).</summary>
    Task<bool> AttributeCodeExistsAsync(string tenantId, string code, int? excludeId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAttributeAsync(CatalogAttributeRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateAttributeAsync(CatalogAttributeRow row, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete attribute. Values cascade via FK. Returns false if not found.</summary>
    Task<bool> DeleteAttributeAsync(int id, string tenantId, CancellationToken cancellationToken = default);

    // ── Attribute value methods ───────────────────────────────────────────────

    Task<IReadOnlyList<CatalogAttributeValueRow>> ListAttributeValuesAsync(int attributeId, string tenantId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAttributeValueAsync(CatalogAttributeValueRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateAttributeValueAsync(CatalogAttributeValueRow row, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAttributeValueAsync(int valueId, int attributeId, string tenantId,
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

    /// <summary>
    /// Products in <c>pending_approval</c> for tenant/store, with submitter from latest <c>submitted</c> comment.
    /// Cursor: <paramref name="afterProductId"/> — return rows with <c>Id &gt; afterProductId</c> (lexicographic).
    /// </summary>
    Task<(IReadOnlyList<PendingApprovalListRow> Items, int TotalCount, string? NextCursor)> ListPendingApprovalsForStoreAsync(
        string tenantId,
        string storeId,
        int limit,
        string? afterProductId,
        CancellationToken cancellationToken = default);
}
