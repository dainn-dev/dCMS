namespace dCMS.Catalog.Api;

/// <summary>Local filesystem root for product image bytes (US-14 stand-in for S3 direct upload).</summary>
public sealed class CatalogMediaOptions
{
    /// <summary>Optional override; otherwise <c>App_Data/dcms-media</c> under the API content root is used.</summary>
    public string? RootPath { get; set; }

    public long MaxUploadBytes { get; set; } = 15 * 1024 * 1024;
}
