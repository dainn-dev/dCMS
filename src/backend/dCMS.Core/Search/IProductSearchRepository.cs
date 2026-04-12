namespace dCMS.Core.Search;

/// <summary>Loads catalog + cross-service data for building a search document (US-5 / IndexingWorker).</summary>
public interface IProductSearchRepository
{
    Task<ProductIndexPayload?> LoadForIndexAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);
}
