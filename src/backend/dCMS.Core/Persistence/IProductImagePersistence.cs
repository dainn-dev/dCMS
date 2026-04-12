using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

public interface IProductImagePersistence
{
    Task<IReadOnlyList<ProductImageRow>> ListForProductAsync(string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task<ProductImageRow?> FindByChecksumAsync(string productId, string tenantId, string storeId, string checksumSha256,
        CancellationToken cancellationToken = default);

    Task<ProductImageRow> CreatePendingAsync(string productId, string tenantId, string storeId, string checksumSha256,
        string imageType, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<int> MarkUploadCompleteAsync(string imageId, string productId, string tenantId, string storeId,
        string storageKey, long contentLength, CancellationToken cancellationToken = default);

    Task<int> DeleteForProductAsync(string imageId, string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task ReorderAsync(string productId, string tenantId, string storeId, IReadOnlyList<string> orderedImageIds,
        CancellationToken cancellationToken = default);

    Task SetPrimaryAsync(string imageId, string productId, string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task<int> UpdateImageTypeAsync(string imageId, string productId, string tenantId, string storeId, string imageType,
        CancellationToken cancellationToken = default);
}
