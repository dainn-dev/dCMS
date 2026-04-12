using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace dCMS.Catalog.Api.Storage;

public sealed class ProductImageS3Signer(IOptions<ProductImageS3Options> options)
{
    private readonly ProductImageS3Options _o = options?.Value ?? new ProductImageS3Options();

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(_o.Bucket) && !string.IsNullOrWhiteSpace(_o.Region);

    public string BuildObjectKey(string tenantId, string storeId, string productId, string imageId)
    {
        var prefix = (_o.KeyPrefix ?? "product-images").Trim().Trim('/');
        return $"{prefix}/{tenantId}/{storeId}/{productId}/{imageId}";
    }

    public string StorageKeyUri(string objectKey) => $"s3://{_o.Bucket}/{objectKey}";

    public string? TryGetPresignedPutUrl(string objectKey, string contentType, out string? errorMessage)
    {
        errorMessage = null;
        if (!IsEnabled)
        {
            errorMessage = "S3 is not configured.";
            return null;
        }

        try
        {
            using var client = CreateClient();
            var expires = DateTime.UtcNow.AddMinutes(_o.PresignedExpiryMinutes <= 0 ? 15 : _o.PresignedExpiryMinutes);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _o.Bucket!,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = expires,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
            };
            return client.GetPreSignedURL(request);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return null;
        }
    }

    public async Task<(bool Found, long ContentLength, string? Error)> TryHeadObjectAsync(string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return (false, 0, "S3 is not configured.");

        try
        {
            using var client = CreateClient();
            var resp = await client.GetObjectMetadataAsync(_o.Bucket, objectKey, cancellationToken)
                .ConfigureAwait(false);
            return (true, resp.ContentLength, null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (false, 0, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    private AmazonS3Client CreateClient()
    {
        var region = RegionEndpoint.GetBySystemName(_o.Region!.Trim());
        if (!string.IsNullOrWhiteSpace(_o.AccessKeyId) && !string.IsNullOrWhiteSpace(_o.SecretAccessKey))
            return new AmazonS3Client(_o.AccessKeyId, _o.SecretAccessKey, region);
        return new AmazonS3Client(region);
    }
}
