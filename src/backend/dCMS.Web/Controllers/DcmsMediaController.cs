using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

/// <summary>
/// Backoffice media upload for brand/category images (DAI Phase 1).
/// Saves uploads under <c>wwwroot/media/{folder}/{tenant}/</c> and returns a public URL string
/// (e.g. <c>/media/brands/t1/ab12….png</c>) that callers store on the entity instead of a base64 blob.
/// Served directly by dCMS.Web (NOT via the gateway).
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/media")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class DcmsMediaController : ControllerBase
{
    private const long MaxImageBytes = 5L * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> ExtByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"]  = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"]  = ".gif",
    };

    private readonly IWebHostEnvironment _env;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IConfiguration _configuration;

    public DcmsMediaController(
        IWebHostEnvironment env,
        IBackOfficeSecurityAccessor security,
        IConfiguration configuration)
    {
        _env = env;
        _security = security;
        _configuration = configuration;
    }

    private string? TenantId => _configuration["Dcms:Estore:TenantId"]?.Trim();

    /// <summary>POST /umbraco/dcms/api/media/upload — multipart image upload, returns { data: { url } }.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImageBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? folder, CancellationToken ct)
    {
        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null) return Unauthorized(Envelope(null, "Not authenticated."));

        if (file is null || file.Length == 0)
            return BadRequest(Envelope(null, "An image file is required."));
        if (file.Length > MaxImageBytes)
            return BadRequest(Envelope(null, "Image exceeds the 5 MB limit."));
        if (!ExtByContentType.TryGetValue(file.ContentType, out var ext))
            return BadRequest(Envelope(null, "Unsupported image type. Use JPG, PNG, WEBP, or GIF."));

        var tenantSeg = Sanitize(TenantId) ?? "shared";
        var folderSeg = Sanitize(folder) ?? "brands";
        var fileName = $"{Guid.NewGuid():N}{ext}";

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "media", folderSeg, tenantSeg);
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, fileName);

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await file.CopyToAsync(fs, ct).ConfigureAwait(false);

        var url = $"/media/{folderSeg}/{tenantSeg}/{fileName}";
        return Ok(Envelope(new { url }, null));
    }

    /// <summary>Keep only URL-safe path segments to avoid traversal / invalid filesystem chars.</summary>
    private static string? Sanitize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var cleaned = new string(s.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    /// <summary>Matches the SPA's { data, meta, error } response envelope.</summary>
    private static object Envelope(object? data, string? error)
        => new { data, meta = (object?)null, error = error is null ? null : new { message = error } };
}
