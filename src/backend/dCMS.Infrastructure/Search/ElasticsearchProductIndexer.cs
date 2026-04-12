using dCMS.Core.Persistence;
using dCMS.Core.Search;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Search;

/// <summary>US-5: full <see cref="ProductDocument"/> upsert/delete in Elasticsearch + Redis cache invalidation.</summary>
public sealed class ElasticsearchProductIndexer(
    ElasticsearchClientFactory elasticsearchFactory,
    IProductSearchRepository productSearchRepository,
    ICatalogSearchCacheInvalidator cacheInvalidator,
    ICatalogPersistence catalogPersistence,
    ILogger<ElasticsearchProductIndexer> logger)
{
    public async Task IndexProductAsync(string tenantId, string storeId, string productId,
        CancellationToken cancellationToken = default)
    {
        var payload = await productSearchRepository
            .LoadForIndexAsync(productId, tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
            return;

        var doc = ProductDocumentBuilder.Build(payload);
        var client = elasticsearchFactory.Client;
        var index = ElasticsearchIndexNames.Products(tenantId);
        await EnsureIndexAsync(client, tenantId, cancellationToken).ConfigureAwait(false);
        IndexName idx = index;
        Id id = doc.Id;
        await client.IndexAsync(doc, idx, id, cancellationToken: cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidateAfterIndexChangeAsync(storeId, doc.Slug, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteProductAsync(string tenantId, string storeId, string productId,
        CancellationToken cancellationToken = default)
    {
        var product = await catalogPersistence.GetByIdAsync(productId, tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (product is not null && !string.Equals(product.StoreId, storeId, StringComparison.Ordinal))
            return;

        string? slug = product is not null && string.Equals(product.StoreId, storeId, StringComparison.Ordinal)
            ? product.Slug
            : null;

        var client = elasticsearchFactory.Client;
        var index = ElasticsearchIndexNames.Products(tenantId);
        await EnsureIndexAsync(client, tenantId, cancellationToken).ConfigureAwait(false);
        IndexName idx = index;
        Id id = productId;
        await client.DeleteAsync(idx, id, cancellationToken: cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidateAfterIndexChangeAsync(storeId, slug, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task EnsureIndexAsync(ElasticsearchClient client, string tenantId,
        CancellationToken cancellationToken) =>
        ProductSearchIndexAliasBootstrap.EnsureForTenantAsync(client, tenantId, logger, cancellationToken);
}
