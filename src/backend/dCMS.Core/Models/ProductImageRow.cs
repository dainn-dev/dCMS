namespace dCMS.Core.Models;

public sealed record ProductImageRow(
    string Id,
    string ProductId,
    string StorageKey,
    string ChecksumSha256,
    int SortOrder,
    bool IsPrimary,
    string ImageType,
    string UploadStatus,
    long ContentLength,
    DateTimeOffset CreatedAt);
