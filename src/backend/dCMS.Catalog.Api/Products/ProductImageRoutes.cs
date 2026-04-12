using System.Security.Cryptography;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Catalog.Api.Storage;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace dCMS.Catalog.Api.Products;

public static class ProductImageRoutes
{
    public static void MapProductImageRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/products/{productId}/images")
            .WithTags("catalog-product-images")
            .WithTenantStoreAccess(configuration);

        Auth(g.MapGet("", ListImages), auth, write: false);
        Auth(g.MapGet("checksum-check", ChecksumCheck), auth, write: false);
        Auth(g.MapPost("presign", Presign), auth, write: true);
        Auth(g.MapPatch("order", Reorder), auth, write: true);
        Auth(g.MapDelete("{imageId}", DeleteImage), auth, write: true);
        Auth(g.MapPatch("{imageId}", PatchImage), auth, write: true);
        Auth(g.MapPost("{imageId}/s3-complete", S3Complete), auth, write: true);
        Auth(g.MapPut("{imageId}/content", UploadContent), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder builder, bool authEnabled, bool write) =>
        authEnabled
            ? builder.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : builder;

    private static async Task<IResult> ListImages(
        string tenantId,
        string storeId,
        string productId,
        ProductService products,
        IProductImagePersistence images,
        CancellationToken cancellationToken)
    {
        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var list = await images.ListForProductAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new
        {
            items = list.Select(i => new
            {
                id = i.Id,
                storageKey = i.StorageKey,
                checksumSha256 = i.ChecksumSha256,
                sortOrder = i.SortOrder,
                isPrimary = i.IsPrimary,
                imageType = i.ImageType,
                uploadStatus = i.UploadStatus,
                contentLength = i.ContentLength,
                createdAt = i.CreatedAt
            }).ToList()
        });
    }

    private static async Task<IResult> ChecksumCheck(
        string tenantId,
        string storeId,
        string productId,
        string? checksum,
        ProductService products,
        IProductImagePersistence images,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(checksum) || checksum.Length != 64 || !IsHex64(checksum))
            return ApiEnvelope.Error("validation_error", "checksum query must be a 64-char hex SHA-256.",
                StatusCodes.Status400BadRequest);

        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var existing = await images.FindByChecksumAsync(productId, tenantId, storeId, checksum.ToLowerInvariant(),
            cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new
        {
            exists = existing is not null,
            image = existing is null
                ? null
                : new
                {
                    id = existing.Id,
                    sortOrder = existing.SortOrder,
                    isPrimary = existing.IsPrimary,
                    imageType = existing.ImageType,
                    uploadStatus = existing.UploadStatus
                }
        });
    }

    private static async Task<IResult> Presign(
        string tenantId,
        string storeId,
        string productId,
        PresignBody body,
        ProductService products,
        IProductImagePersistence images,
        ProductImageS3Signer s3Signer,
        CancellationToken cancellationToken)
    {
        if (body.ChecksumSha256 is null || body.ChecksumSha256.Length != 64 || !IsHex64(body.ChecksumSha256))
            return ApiEnvelope.Error("validation_error", "checksumSha256 must be a 64-char hex SHA-256.",
                StatusCodes.Status400BadRequest);

        var imageType = NormalizeImageType(body.ImageType);
        if (imageType is null)
            return ApiEnvelope.Error("validation_error", "imageType must be main, gallery, or swatch.",
                StatusCodes.Status400BadRequest);

        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var checksum = body.ChecksumSha256.ToLowerInvariant();
        var dup = await images.FindByChecksumAsync(productId, tenantId, storeId, checksum, cancellationToken)
            .ConfigureAwait(false);
        if (dup is not null)
            return Results.Json(
                new
                {
                    data = (object?)null,
                    meta = new { existingImageId = dup.Id },
                    error = new
                    {
                        code = "duplicate_image_checksum",
                        message = "An image with this checksum already exists for the product."
                    }
                }, statusCode: StatusCodes.Status409Conflict);

        try
        {
            var row = await images.CreatePendingAsync(productId, tenantId, storeId, checksum, imageType,
                DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            var contentType = string.IsNullOrWhiteSpace(body.ContentType) ? "application/octet-stream" : body.ContentType.Trim();
            if (s3Signer.IsEnabled)
            {
                var objectKey = s3Signer.BuildObjectKey(tenantId, storeId, productId, row.Id);
                var uploadUrl = s3Signer.TryGetPresignedPutUrl(objectKey, contentType, out var s3Err);
                if (uploadUrl is null)
                    return ApiEnvelope.Error("s3_presign_failed", s3Err ?? "Unknown S3 error.",
                        StatusCodes.Status500InternalServerError);

                return ApiEnvelope.Ok(new
                {
                    imageId = row.Id,
                    uploadUrl,
                    method = "PUT",
                    headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = contentType },
                    uploadMode = "s3",
                    storageKey = s3Signer.StorageKeyUri(objectKey),
                    note =
                        "PUT uploadUrl from the browser, then POST …/images/{imageId}/s3-complete via the BFF. The S3 bucket CORS must allow PUT from the backoffice origin and expose ETag if needed."
                });
            }

            var path = $"products/{productId}/images/{row.Id}/content";
            return ApiEnvelope.Ok(new
            {
                imageId = row.Id,
                method = "PUT",
                path,
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = contentType },
                uploadMode = "catalog-put",
                note =
                    "Upload bytes with PUT to this path on Catalog.Api (Bearer JWT). Umbraco BFF supports base64 via CatalogBackofficeProxy Forward.binaryBodyBase64."
            });
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ApiEnvelope.Error("duplicate_image_checksum", "An image with this checksum already exists for the product.",
                StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> Reorder(
        string tenantId,
        string storeId,
        string productId,
        ImageOrderBody body,
        ProductService products,
        IProductImagePersistence images,
        CancellationToken cancellationToken)
    {
        if (body.ImageIds is null || body.ImageIds.Count == 0)
            return ApiEnvelope.Error("validation_error", "imageIds is required.", StatusCodes.Status400BadRequest);

        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        try
        {
            await images.ReorderAsync(productId, tenantId, storeId, body.ImageIds, cancellationToken).ConfigureAwait(false);
            await products.TouchProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { productId });
        }
        catch (InvalidOperationException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeleteImage(
        string tenantId,
        string storeId,
        string productId,
        string imageId,
        ProductService products,
        IProductImagePersistence images,
        CancellationToken cancellationToken)
    {
        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var n = await images.DeleteForProductAsync(imageId, productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (n == 0)
            return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);

        await products.TouchProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(new { id = imageId });
    }

    private static async Task<IResult> PatchImage(
        string tenantId,
        string storeId,
        string productId,
        string imageId,
        PatchImageBody body,
        ProductService products,
        IProductImagePersistence images,
        CancellationToken cancellationToken)
    {
        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var any = false;
        try
        {
            if (body.IsPrimary == true)
            {
                await images.SetPrimaryAsync(imageId, productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
                any = true;
            }

            if (body.CycleType == true)
            {
                var list = await images.ListForProductAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
                var cur = list.FirstOrDefault(i => i.Id == imageId);
                if (cur is null)
                    return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);
                var next = cur.ImageType.ToLowerInvariant() switch
                {
                    "main" => "gallery",
                    "gallery" => "swatch",
                    _ => "main"
                };
                var u = await images.UpdateImageTypeAsync(imageId, productId, tenantId, storeId, next, cancellationToken)
                    .ConfigureAwait(false);
                if (u == 0)
                    return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);
                any = true;
            }
            else if (!string.IsNullOrWhiteSpace(body.ImageType))
            {
                var t = NormalizeImageType(body.ImageType);
                if (t is null)
                    return ApiEnvelope.Error("validation_error", "imageType must be main, gallery, or swatch.",
                        StatusCodes.Status400BadRequest);
                var updated = await images.UpdateImageTypeAsync(imageId, productId, tenantId, storeId, t, cancellationToken)
                    .ConfigureAwait(false);
                if (updated == 0)
                    return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);
                any = true;
            }

            if (!any)
                return ApiEnvelope.Error("validation_error", "Provide isPrimary, imageType, or cycleType.",
                    StatusCodes.Status400BadRequest);

            await products.TouchProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id = imageId });
        }
        catch (InvalidOperationException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> S3Complete(
        string tenantId,
        string storeId,
        string productId,
        string imageId,
        S3CompleteBody? body,
        ProductService products,
        IProductImagePersistence images,
        ProductImageS3Signer s3Signer,
        IOptions<CatalogMediaOptions> mediaOptions,
        CancellationToken cancellationToken)
    {
        if (!s3Signer.IsEnabled)
            return ApiEnvelope.Error("s3_disabled", "S3 direct upload is not configured for this API.",
                StatusCodes.Status400BadRequest);

        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var list = await images.ListForProductAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var meta = list.FirstOrDefault(i => i.Id == imageId);
        if (meta is null)
            return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);
        if (!string.Equals(meta.UploadStatus, "pending", StringComparison.OrdinalIgnoreCase))
            return ApiEnvelope.Error("invalid_state", "Image upload already completed.", StatusCodes.Status400BadRequest);

        var objectKey = s3Signer.BuildObjectKey(tenantId, storeId, productId, imageId);
        var (found, length, headErr) = await s3Signer.TryHeadObjectAsync(objectKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(headErr))
            return ApiEnvelope.Error("s3_head_failed", headErr, StatusCodes.Status502BadGateway);
        if (!found)
            return ApiEnvelope.Error("s3_object_missing", "Object not found in S3 after upload.",
                StatusCodes.Status400BadRequest);
        if (length <= 0)
            return ApiEnvelope.Error("s3_empty_object", "S3 object has zero length.", StatusCodes.Status400BadRequest);

        var max = Math.Clamp(mediaOptions.Value.MaxUploadBytes, 64 * 1024, 50L * 1024 * 1024);
        if (length > max)
            return ApiEnvelope.Error("validation_error", $"S3 object exceeds max of {max} bytes.",
                StatusCodes.Status400BadRequest);

        if (body?.ContentLength is > 0 && body.ContentLength != length)
            return ApiEnvelope.Error("content_length_mismatch", "contentLength does not match the object in S3.",
                StatusCodes.Status400BadRequest);

        var storageKey = s3Signer.StorageKeyUri(objectKey);
        var updated = await images.MarkUploadCompleteAsync(imageId, productId, tenantId, storeId, storageKey, length,
            cancellationToken).ConfigureAwait(false);
        if (updated == 0)
            return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);

        await products.TouchProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(new { id = imageId, storageKey, contentLength = length });
    }

    private static async Task<IResult> UploadContent(
        string tenantId,
        string storeId,
        string productId,
        string imageId,
        HttpRequest request,
        ProductService products,
        IProductImagePersistence images,
        IOptions<CatalogMediaOptions> mediaOptions,
        IHostEnvironment env,
        CancellationToken cancellationToken)
    {
        if (await products.GetProductForStoreAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false) is null)
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var list = await images.ListForProductAsync(productId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var meta = list.FirstOrDefault(i => i.Id == imageId);
        if (meta is null)
            return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);
        if (!string.Equals(meta.UploadStatus, "pending", StringComparison.OrdinalIgnoreCase))
            return ApiEnvelope.Error("invalid_state", "Image upload already completed.", StatusCodes.Status400BadRequest);

        var root = ResolveMediaRoot(mediaOptions.Value, env);
        Directory.CreateDirectory(root);
        var dir = Path.Combine(root, tenantId, storeId, productId);
        Directory.CreateDirectory(dir);
        var physicalPath = Path.Combine(dir, imageId + ".bin");
        var storageKey = $"{tenantId}/{storeId}/{productId}/{imageId}.bin";

        var max = Math.Clamp(mediaOptions.Value.MaxUploadBytes, 64 * 1024, 50L * 1024 * 1024);
        await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            using var sha = SHA256.Create();
            var buffer = new byte[65536];
            long total = 0;
            while (true)
            {
                var read = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;
                total += read;
                if (total > max)
                    return ApiEnvelope.Error("validation_error", $"Upload exceeds max of {max} bytes.",
                        StatusCodes.Status400BadRequest);
                sha.TransformBlock(buffer, 0, read, null, 0);
                await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            if (!string.Equals(hash, meta.ChecksumSha256, StringComparison.Ordinal))
            {
                try
                {
                    File.Delete(physicalPath);
                }
                catch
                {
                    // ignore
                }

                return ApiEnvelope.Error("checksum_mismatch", "Uploaded bytes do not match declared SHA-256.",
                    StatusCodes.Status400BadRequest);
            }
        }

        var len = new FileInfo(physicalPath).Length;
        var updated = await images.MarkUploadCompleteAsync(imageId, productId, tenantId, storeId, storageKey, len,
            cancellationToken).ConfigureAwait(false);
        if (updated == 0)
            return ApiEnvelope.Error("not_found", "Image not found.", StatusCodes.Status404NotFound);

        await products.TouchProductAsync(productId, tenantId, storeId, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(new { id = imageId, storageKey = storageKey, contentLength = len });
    }

    private static string ResolveMediaRoot(CatalogMediaOptions o, IHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(o.RootPath))
            return Path.IsPathRooted(o.RootPath!) ? o.RootPath! : Path.Combine(env.ContentRootPath, o.RootPath!);
        return Path.Combine(env.ContentRootPath, "App_Data", "dcms-media");
    }

    private static bool IsHex64(string s)
    {
        foreach (var c in s)
        {
            if (c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')
                continue;
            return false;
        }

        return true;
    }

    private static string? NormalizeImageType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "gallery";
        var t = raw.Trim().ToLowerInvariant();
        return t is "main" or "gallery" or "swatch" ? t : null;
    }

    public sealed record PresignBody(string? ChecksumSha256, string? ContentType, string? FileName, string? ImageType);

    public sealed record ImageOrderBody(List<string>? ImageIds);

    public sealed record PatchImageBody(bool? IsPrimary, string? ImageType, bool? CycleType);

    public sealed record S3CompleteBody(long? ContentLength);
}
