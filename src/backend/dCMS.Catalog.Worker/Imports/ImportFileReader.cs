using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dCMS.Catalog.Worker.Imports;

// DAI-707 — worker-side reader for files uploaded by Catalog.Api/ImportFileStore.
// Same env-vars (Catalog:S3:ImportFiles + Catalog:Media:RootPath) so dev/local
// filesystem fallback stays in sync with the API process.
public sealed class ImportFileReader
{
    private readonly ImportFileReaderOptions _o;
    private readonly CatalogMediaPathOptions _media;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ImportFileReader> _log;

    public ImportFileReader(
        IOptions<ImportFileReaderOptions> options,
        IOptions<CatalogMediaPathOptions> mediaOptions,
        IHostEnvironment env,
        ILogger<ImportFileReader> log)
    {
        _o = options?.Value ?? new ImportFileReaderOptions();
        _media = mediaOptions?.Value ?? new CatalogMediaPathOptions();
        _env = env;
        _log = log;
    }

    private bool IsS3Enabled => !string.IsNullOrWhiteSpace(_o.Bucket) && !string.IsNullOrWhiteSpace(_o.Region);

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct)
    {
        if (IsS3Enabled)
        {
            var client = CreateClient();
            var resp = await client.GetObjectAsync(_o.Bucket!, objectKey, ct).ConfigureAwait(false);
            return resp.ResponseStream;
        }

        var localPath = ResolveLocalPath(objectKey);
        if (!File.Exists(localPath))
        {
            _log.LogError("Import file not found at {Path} (key={Key}).", localPath, objectKey);
            throw new FileNotFoundException($"Import file not found: {objectKey}", localPath);
        }
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
            RegionEndpoint = RegionEndpoint.GetBySystemName(_o.Region!.Trim())
        };
        if (!string.IsNullOrWhiteSpace(_o.ServiceUrl))
        {
            cfg.ServiceURL = _o.ServiceUrl;
            cfg.ForcePathStyle = true;
        }
        if (!string.IsNullOrWhiteSpace(_o.AccessKeyId) && !string.IsNullOrWhiteSpace(_o.SecretAccessKey))
            return new AmazonS3Client(_o.AccessKeyId, _o.SecretAccessKey, cfg);
        return new AmazonS3Client(cfg);
    }
}

public sealed class ImportFileReaderOptions
{
    public string? Bucket { get; set; }
    public string? Region { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string? ServiceUrl { get; set; }
}

public sealed class CatalogMediaPathOptions
{
    public string? RootPath { get; set; }
}
