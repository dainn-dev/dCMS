using dCMS.Core.Persistence;
using dCMS.Core.Search;
using dCMS.Infrastructure.Search;
using Microsoft.Extensions.Logging;

namespace dCMS.Catalog.Api.Stores;

/// <summary>
/// After Product Configuration schema changes: bust storefront caches and re-index all store products in ES
/// so <c>attributes</c> reflect the latest custom-field values/properties.
/// </summary>
public sealed class ProductFieldConfigSyncService(
    ICatalogPersistence catalog,
    ElasticsearchProductIndexer indexer,
    ICatalogSearchCacheInvalidator cacheInvalidator,
    ILogger<ProductFieldConfigSyncService> logger)
{
    public async Task SyncStoreAfterConfigChangeAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        await cacheInvalidator.InvalidateStoreAsync(storeId, cancellationToken).ConfigureAwait(false);

        var productIds = await catalog
            .ListProductIdsForStoreAsync(tenantId, storeId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Re-indexing {Count} products for tenant {TenantId} store {StoreId} after product-field-config change.",
            productIds.Count, tenantId, storeId);

        var indexed = 0;
        foreach (var productId in productIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await indexer.IndexProductAsync(tenantId, storeId, productId, cancellationToken).ConfigureAwait(false);
                indexed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to re-index product {ProductId} after field-config change.", productId);
            }
        }

        logger.LogInformation(
            "Product-field-config sync complete for store {StoreId}: {Indexed}/{Total} products re-indexed.",
            storeId, indexed, productIds.Count);
    }
}
