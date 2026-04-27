using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Catalog.Api.Storage;
using dCMS.Core.Messaging;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace dCMS.Catalog.Api.Imports;

// DAI-684 / DAI-706 — bulk import endpoints (upload + status + list).
// POST   /api/v1/tenants/{tenantId}/imports               (multipart: file, type)
// GET    /api/v1/tenants/{tenantId}/imports/{jobId}
// GET    /api/v1/tenants/{tenantId}/imports
public static class ImportJobRoutes
{
    private static readonly string[] AllowedContentTypes =
    [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "text/csv",
        "application/csv",
        "application/octet-stream"
    ];

    public static void MapImportJobRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/imports")
            .WithTags("catalog-imports")
            .WithTenantAccess(configuration);

        Auth(g.MapPost("", UploadAsync).DisableAntiforgery(), auth);
        Auth(g.MapGet("{jobId}", GetStatusAsync), auth);
        Auth(g.MapGet("", ListAsync), auth);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled) =>
        authEnabled ? b.RequireAuthorization(DcmsPolicies.CatalogImport) : b;

    private static async Task<IResult> UploadAsync(
        string tenantId,
        HttpRequest request,
        IImportJobPersistence repo,
        ImportFileStore files,
        IPublishEndpoint bus,
        IOptions<CatalogImportOptions> opts,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
            return ApiEnvelope.Error("validation_error", "multipart/form-data required.",
                StatusCodes.Status415UnsupportedMediaType);

        var maxBytes = opts.Value?.MaxFileBytes is > 0 ? opts.Value!.MaxFileBytes : 50L * 1024 * 1024;

        var form = await request.ReadFormAsync(ct).ConfigureAwait(false);
        var type = form["type"].ToString().Trim();
        if (!ImportJobTypes.IsValid(type))
            return ApiEnvelope.Error("validation_error",
                "type must be one of products, product-images, inventory, promo-codes.",
                StatusCodes.Status400BadRequest);

        var file = form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return ApiEnvelope.Error("validation_error", "file is required.", StatusCodes.Status400BadRequest);
        if (file.Length > maxBytes)
            return ApiEnvelope.Error("validation_error", $"file exceeds max size of {maxBytes} bytes.",
                StatusCodes.Status400BadRequest);

        var ct0 = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType.ToLowerInvariant();
        if (!AllowedContentTypes.Contains(ct0))
            return ApiEnvelope.Error("validation_error", $"unsupported content-type: {ct0}.",
                StatusCodes.Status415UnsupportedMediaType);

        var jobId = "imp_" + NewId.NextSequentialGuid().ToString("N")[..20];
        var fileName = string.IsNullOrWhiteSpace(file.FileName) ? "source.bin" : file.FileName;
        var key = files.BuildObjectKey(tenantId, jobId, fileName);

        await using (var src = file.OpenReadStream())
        {
            await files.UploadAsync(src, key, ct0, ct).ConfigureAwait(false);
        }

        var now = DateTimeOffset.UtcNow;
        var job = new ImportJob(
            jobId, tenantId, type, ImportJobStatuses.Pending, key,
            Total: null, Processed: 0, Errors: Array.Empty<ImportRowError>(), LastProcessedKey: null,
            CreatedBy: request.HttpContext.User.Identity?.Name ?? "system",
            CreatedAt: now, StartedAt: null, CompletedAt: null);
        await repo.CreateAsync(job, ct).ConfigureAwait(false);

        await bus.Publish(new ImportJobQueuedV1(jobId, tenantId, type, key, now), ct).ConfigureAwait(false);

        var statusUrl = $"/api/v1/tenants/{tenantId}/imports/{jobId}";
        return Results.Json(new
        {
            data = new { jobId, statusUrl, type, status = ImportJobStatuses.Pending },
            meta = (object?)null,
            error = (object?)null
        }, statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> GetStatusAsync(
        string tenantId,
        string jobId,
        IImportJobPersistence repo,
        CancellationToken ct)
    {
        var job = await repo.GetAsync(jobId, tenantId, ct).ConfigureAwait(false);
        if (job is null)
            return ApiEnvelope.Error("not_found", "Import job not found.", StatusCodes.Status404NotFound);
        return ApiEnvelope.Ok(ToDto(job));
    }

    private static async Task<IResult> ListAsync(
        string tenantId,
        IImportJobPersistence repo,
        int? take,
        CancellationToken ct)
    {
        var rows = await repo.ListRecentAsync(tenantId, take ?? 50, ct).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { items = rows.Select(ToDto).ToList() });
    }

    private static object ToDto(ImportJob j) => new
    {
        id = j.Id,
        type = j.Type,
        status = j.Status,
        total = j.Total,
        processed = j.Processed,
        errors = j.Errors.Select(e => new { rowIndex = e.RowIndex, key = e.Key, message = e.Message }).ToList(),
        createdAt = j.CreatedAt,
        startedAt = j.StartedAt,
        completedAt = j.CompletedAt
    };
}
