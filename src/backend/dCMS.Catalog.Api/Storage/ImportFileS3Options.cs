namespace dCMS.Catalog.Api.Storage;

// DAI-684 — S3 location for raw import files. Falls back to ProductImageS3Options bucket
// (different prefix) if Bucket is empty so dev/MinIO with a single bucket still works.
public sealed class ImportFileS3Options
{
    public string? Bucket { get; set; }
    public string? Region { get; set; }
    public string KeyPrefix { get; set; } = "imports";
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string? ServiceUrl { get; set; }
}

public sealed class CatalogImportOptions
{
    public long MaxFileBytes { get; set; } = 50L * 1024 * 1024;
}
