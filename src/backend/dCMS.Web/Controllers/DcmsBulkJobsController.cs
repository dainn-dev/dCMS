using dCMS.Core.Persistence;
using dCMS.Web.BulkJobs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

/// <summary>Backoffice APIs for async bulk catalog import and order export (DAI-684 / Hangfire).</summary>
[ApiController]
[Route("umbraco/dcms/api/bulk-jobs")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class DcmsBulkJobsController : ControllerBase
{
    private const long MaxUploadBytes = 50L * 1024 * 1024;

    private readonly IBulkJobRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IServiceProvider _services;

    public DcmsBulkJobsController(
        IBulkJobRepository repo,
        IConfiguration configuration,
        IWebHostEnvironment env,
        IBackOfficeSecurityAccessor security,
        IServiceProvider services)
    {
        _repo = repo;
        _configuration = configuration;
        _env = env;
        _security = security;
        _services = services;
    }

    private string? TenantId => _configuration["Dcms:Estore:TenantId"]?.Trim();

    // ── POST catalog-import ────────────────────────────────────────────────
    [HttpPost("catalog-import")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> StartCatalogImport([FromForm] IFormFile? file, [FromForm] string? storeId, CancellationToken ct)
    {
        if (_services.GetService<ICatalogPersistence>() is null)
            return StatusCode(503, Env("Catalog connection is not configured on this host; catalog import is unavailable."));

        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null) return Unauthorized(Env("Not authenticated."));

        if (file is null || file.Length == 0)
            return BadRequest(Env("CSV file is required."));

        var jobId = Guid.NewGuid();
        BulkJobFileStorage.EnsureJobDir(_env.ContentRootPath, tenantId, jobId);
        var fullPath = BulkJobFileStorage.GetInputFilePath(_env.ContentRootPath, tenantId, jobId);
        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await file.CopyToAsync(fs, ct).ConfigureAwait(false);

        var rel = BulkJobFileStorage.GetInputRelativeKey(tenantId, jobId);
        var now = DateTimeOffset.UtcNow;
        await _repo.InsertAsync(new BulkJobRecord
        {
            Id = jobId,
            TenantId = tenantId,
            StoreId = string.IsNullOrWhiteSpace(storeId) ? null : storeId.Trim(),
            JobKind = BulkJobKinds.CatalogImport,
            RequestedByUserId = user.Id,
            Status = "queued",
            ProgressProcessed = 0,
            ProgressTotal = 0,
            ProgressPercent = 0,
            InputBlobRef = rel,
            CreatedAt = now,
        }, ct).ConfigureAwait(false);

        BulkJobMetrics.Started.WithLabels(tenantId, BulkJobKinds.CatalogImport).Inc();
        var hf = BackgroundJob.Enqueue<CatalogBulkJobRunner>(x => x.RunAsync(tenantId, jobId));
        await _repo.SetHangfireJobIdAsync(tenantId, jobId, hf, ct).ConfigureAwait(false);

        return Ok(new { data = new { jobId = jobId.ToString(), hangfireJobId = hf }, meta = (object?)null, error = (object?)null });
    }

    // ── POST orders-export ──────────────────────────────────────────────────
    [HttpPost("orders-export")]
    public async Task<IActionResult> StartOrdersExport([FromBody] OrdersExportRequest body, CancellationToken ct)
    {
        var orderCs = _configuration.GetConnectionString("Order");
        if (string.IsNullOrWhiteSpace(orderCs))
            return StatusCode(503, Env("Order connection is not configured; export is unavailable."));

        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null) return Unauthorized(Env("Not authenticated."));

        if (body.DateTo < body.DateFrom)
            return BadRequest(Env("dateTo must be >= dateFrom."));

        var jobId = Guid.NewGuid();
        BulkJobFileStorage.EnsureJobDir(_env.ContentRootPath, tenantId, jobId);
        var jsonPath = Path.Combine(BulkJobFileStorage.GetJobDir(_env.ContentRootPath, tenantId, jobId), "input.json");
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        await System.IO.File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);

        var rel = BulkJobFileStorage.GetOrdersInputRelativeKey(tenantId, jobId);
        var now = DateTimeOffset.UtcNow;
        await _repo.InsertAsync(new BulkJobRecord
        {
            Id = jobId,
            TenantId = tenantId,
            StoreId = string.IsNullOrWhiteSpace(body.StoreId) ? null : body.StoreId.Trim(),
            JobKind = BulkJobKinds.OrdersExport,
            RequestedByUserId = user.Id,
            Status = "queued",
            ProgressProcessed = 0,
            ProgressTotal = 0,
            ProgressPercent = 0,
            InputBlobRef = rel,
            CreatedAt = now,
        }, ct).ConfigureAwait(false);

        BulkJobMetrics.Started.WithLabels(tenantId, BulkJobKinds.OrdersExport).Inc();
        var hf = BackgroundJob.Enqueue<OrdersBulkJobRunner>(x => x.RunAsync(tenantId, jobId));
        await _repo.SetHangfireJobIdAsync(tenantId, jobId, hf, ct).ConfigureAwait(false);

        return Ok(new { data = new { jobId = jobId.ToString(), hangfireJobId = hf }, meta = (object?)null, error = (object?)null });
    }

    // ── GET list ───────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var items = await _repo.ListByTenantAsync(tenantId, limit, ct).ConfigureAwait(false);
        return Ok(new { data = items.Select(Map), meta = (object?)null, error = (object?)null });
    }

    // ── GET {id} ──────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var j = await _repo.GetByIdAsync(tenantId, id, ct).ConfigureAwait(false);
        if (j is null) return NotFound(Env("Job not found."));
        return Ok(new { data = Map(j), meta = (object?)null, error = (object?)null });
    }

    // ── GET {id}/download ─────────────────────────────────────────────────
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var j = await _repo.GetByIdAsync(tenantId, id, ct).ConfigureAwait(false);
        if (j is null) return NotFound(Env("Job not found."));
        if (j.Status != "succeeded" || string.IsNullOrWhiteSpace(j.OutputBlobRef))
            return Conflict(Env("Export is not ready or job did not produce a file."));

        var full = Path.IsPathRooted(j.OutputBlobRef) ? j.OutputBlobRef : Path.GetFullPath(Path.Combine(_env.ContentRootPath, j.OutputBlobRef));
        if (!System.IO.File.Exists(full))
            return NotFound(Env("Output file missing on disk."));

        return PhysicalFile(full, "text/csv", fileDownloadName: $"orders-export-{id:N}.csv");
    }

    // ── POST {id}/cancel ──────────────────────────────────────────────────
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var j = await _repo.GetByIdAsync(tenantId, id, ct).ConfigureAwait(false);
        if (j is null) return NotFound(Env("Job not found."));

        if (j.Status is "succeeded" or "failed" or "cancelled")
            return Conflict(Env("Job is already in a terminal state."));

        var at = DateTimeOffset.UtcNow;
        if (j.Status == "running")
        {
            await _repo.SetCancelRequestedAsync(tenantId, id, at, ct).ConfigureAwait(false);
        }
        else if (j.Status == "queued")
        {
            if (!string.IsNullOrWhiteSpace(j.HangfireJobId))
                BackgroundJob.Delete(j.HangfireJobId);
            await _repo.MarkFinishedAsync(tenantId, id, "cancelled", at, null, ct).ConfigureAwait(false);
        }

        return Ok(new { data = new { jobId = id.ToString(), cancelled = true }, meta = (object?)null, error = (object?)null });
    }

    // ── POST {id}/retry ─────────────────────────────────────────────────────
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(Env("Dcms:Estore:TenantId is not configured."));

        var j = await _repo.GetByIdAsync(tenantId, id, ct).ConfigureAwait(false);
        if (j is null) return NotFound(Env("Job not found."));
        if (j.Status != "failed")
            return Conflict(Env("Only failed jobs can be retried."));

        var n = await _repo.ResetFailedJobForRetryAsync(tenantId, id, ct).ConfigureAwait(false);
        if (n == 0) return Conflict(Env("Could not reset job for retry."));

        BulkJobMetrics.Started.WithLabels(tenantId, j.JobKind).Inc();
        string hf;
        if (j.JobKind == BulkJobKinds.CatalogImport)
        {
            if (_services.GetService<ICatalogPersistence>() is null)
                return StatusCode(503, Env("Catalog is not configured."));
            hf = BackgroundJob.Enqueue<CatalogBulkJobRunner>(x => x.RunAsync(tenantId, id));
        }
        else if (j.JobKind == BulkJobKinds.OrdersExport)
        {
            hf = BackgroundJob.Enqueue<OrdersBulkJobRunner>(x => x.RunAsync(tenantId, id));
        }
        else
            return BadRequest(Env("Unknown job kind."));

        await _repo.SetHangfireJobIdAsync(tenantId, id, hf, ct).ConfigureAwait(false);
        return Ok(new { data = new { jobId = id.ToString(), hangfireJobId = hf }, meta = (object?)null, error = (object?)null });
    }

    private static object Map(BulkJobRecord j) => new
    {
        id = j.Id.ToString(),
        j.TenantId,
        j.StoreId,
        jobKind = j.JobKind,
        requestedByUserId = j.RequestedByUserId,
        hangfireJobId = j.HangfireJobId,
        j.Status,
        j.ProgressProcessed,
        j.ProgressTotal,
        j.ProgressPercent,
        inputBlobRef = j.InputBlobRef,
        outputBlobRef = j.OutputBlobRef,
        errorMessage = j.ErrorMessage,
        createdAt = j.CreatedAt,
        startedAt = j.StartedAt,
        finishedAt = j.FinishedAt,
        cancelRequestedAt = j.CancelRequestedAt,
    };

    private static object Env(string message) => new { data = (object?)null, meta = (object?)null, error = new { code = "BULK_JOB", message } };
}
