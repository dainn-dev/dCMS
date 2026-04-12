namespace dCMS.Catalog.Api;

/// <summary>Optional S3 direct upload for product images (DAI-290). When <see cref="Bucket"/> is empty, API falls back to Catalog PUT …/content.</summary>
public sealed class ProductImageS3Options
{
    /// <summary>S3 bucket name (e.g. dcms-media-prod). Empty disables S3 presign.</summary>
    public string? Bucket { get; set; }

    /// <summary>AWS region system name (e.g. ap-southeast-1).</summary>
    public string? Region { get; set; }

    /// <summary>Object key prefix without leading slash (e.g. product-images).</summary>
    public string KeyPrefix { get; set; } = "product-images";

    /// <summary>Optional static credentials; if omitted, SDK default chain (env, profile, IAM role).</summary>
    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public int PresignedExpiryMinutes { get; set; } = 15;
}
