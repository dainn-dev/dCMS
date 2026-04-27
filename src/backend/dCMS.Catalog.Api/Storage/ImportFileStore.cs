using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dCMS.Catalog.Api.Storage;

// DAI-684 — S3 wrapper for bulk-import file uploads + worker reads. When S3 is not
// configured, files persist to a local filesystem under MediaRoot/imports for dev.
public sealed class ImportFileStore
{
    private readonly ImportFileS3Options _o;
    private readonly ProductImageS3Options _fallback;
    private readonly CatalogMediaOptions _media;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ImportFileStore> _log;

    public ImportFileStore(
        IOptions<ImportFileS3Options> options,
        IOptions<ProductImageS3Options> fallback,
        IOptions<CatalogMediaOptions> mediaOptions,
        IHostEnvironment env,
        ILogger<ImportFileStore> log)
    {
        _o = options?.Value ?? new ImportFileS3Options();
        _fallback = fallback?.Value ?? new ProductImageS3Options();
        _media = mediaOptions?.Value ?? new CatalogMediaOptions();
        _env = env;
        _log = log;
    }

    public bool IsS3Enabled =>
        !string.IsNullOrWhiteSpace(EffectiveBucket) && !string.IsNullOrWhiteSpace(EffectiveRegion);

    private string? EffectiveBucket =>
        string.IsNullOrWhiteSpace(_o.Bucket) ? _fallback.Bucket : _o.Bucket;

    private string? EffectiveRegion =>
        string.IsNullOrWhiteSpace(_o.Region) ? _fallback.Region : _o.Region;

    public string BuildObjectKey(string tenantId, string jobId, string fileName)
    {
        var prefix = (_o.KeyPrefix ?? "imports").Trim().Trim('/');
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "source.bin" : Path.GetFileName(fileName);
        return $"{prefix}/{tenantId}/{jobId}/{safeName}";
    }

    public async Task UploadAsync(Stream stream, string objectKey, string contentType, CancellationToken ct)
    {
        if (IsS3Enabled)
        {
            using var client = CreateClient();
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = EffectiveBucket!,
                Key = objectKey,
                InputStream = stream,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                AutoCloseStream = false,
                AutoResetStreamPosition = false
            }, ct).ConfigureAwait(false);
            return;
        }

        var localPath = ResolveLocalPath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
        _log.LogInformation("Persisted import file to local path {Path}", localPath);
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct)
    {
        if (IsS3Enabled)
        {
            using var client = CreateClient();
            var resp = await client.GetObjectAsync(EffectiveBucket!, objectKey, ct).ConfigureAwait(false);
            return resp.ResponseStream;
        }

        var localPath = ResolveLocalPath(objectKey);
        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Import file not found: {objectKey}", localPath);
        return new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private string ResolveLocalPath(string objectKey)
    {
        var root = string.IsNullOrWhiteSpace(_media.RootPath)
            ? Path.Combine(_env.ContentRootPath, "App_Data", "dcms-media")
            : (Path.IsPathRooted(_media.RootPath) ? _media.RootPath : Path.Combine(_env.ContentRootPath, _media.RootPath));
        return Path.Combine(root, objectKey.Replace('/', Path.DirectorySeparatorChar));
    }

    private AmazonS3Client CreateClient()
    {
        var cfg = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(EffectiveRegion!.Trim())
        };
        if (!string.IsNullOrWhiteSpace(_o.ServiceUrl))
        {
            cfg.ServiceURL = _o.ServiceUrl;
            cfg.ForcePathStyle = true;
        }
        var keyId = string.IsNullOrWhiteSpace(_o.AccessKeyId) ? _fallback.AccessKeyId : _o.AccessKeyId;
        var secret = string.IsNullOrWhiteSpace(_o.SecretAccessKey) ? _fallback.SecretAccessKey : _o.SecretAccessKey;
        if (!string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(secret))
            return new AmazonS3Client(keyId, secret, cfg);
        return new AmazonS3Client(cfg);
    }
}
