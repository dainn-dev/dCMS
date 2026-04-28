using System.Security.Cryptography;
using System.Text;
using dCMS.Web.ContentApproval;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;

namespace dCMS.Web.Controllers;

/// <summary>
/// DAI-721: Internal HTTP callback used by dCMS.Approval.Api to publish/unpublish Umbraco content
/// after a generic approval transition. Authenticated via shared secret <c>X-Internal-Api-Key</c>
/// (configure <c>ContentApproval:ApiKey</c>) — service-to-service only, not exposed to backoffice users.
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/content-approval")]
public sealed class DcmsContentApprovalController : ControllerBase
{
    private const string HeaderName = "X-Internal-Api-Key";

    private readonly IContentService _contentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DcmsContentApprovalController> _logger;

    public DcmsContentApprovalController(
        IContentService contentService,
        IConfiguration configuration,
        ILogger<DcmsContentApprovalController> logger)
    {
        _contentService = contentService;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed record CallbackPayload(string TenantId, string ContentKey, string ActedByUserId);

    [HttpPost("publish")]
    public IActionResult Publish([FromBody] CallbackPayload? body)
    {
        var auth = CheckApiKey();
        if (auth is not null) return auth;
        if (body is null || !Guid.TryParse(body.ContentKey, out var key))
            return BadRequest(new { error = "ContentKey must be a GUID." });

        var content = _contentService.GetById(key);
        if (content is null)
            return NotFound(new { error = "Content not found." });

        using var _ = ApprovalGate.Bypass();
        var result = _contentService.Publish(content, ["*"], userId: -1);
        if (!result.Success)
        {
            _logger.LogWarning("Approval publish failed for {Key}: {Reason}", key, result.Result);
            return StatusCode(409, new { error = "Publish failed.", reason = result.Result.ToString() });
        }
        return Ok(new { data = new { contentKey = key, status = "published" } });
    }

    [HttpPost("unpublish")]
    public IActionResult Unpublish([FromBody] CallbackPayload? body)
    {
        var auth = CheckApiKey();
        if (auth is not null) return auth;
        if (body is null || !Guid.TryParse(body.ContentKey, out var key))
            return BadRequest(new { error = "ContentKey must be a GUID." });

        var content = _contentService.GetById(key);
        if (content is null)
            return NotFound(new { error = "Content not found." });

        using var _ = ApprovalGate.Bypass();
        var result = _contentService.Unpublish(content, userId: -1);
        if (!result.Success)
        {
            _logger.LogWarning("Approval unpublish failed for {Key}: {Reason}", key, result.Result);
            return StatusCode(409, new { error = "Unpublish failed.", reason = result.Result.ToString() });
        }
        return Ok(new { data = new { contentKey = key, status = "unpublished" } });
    }

    private IActionResult? CheckApiKey()
    {
        var configured = _configuration["ContentApproval:ApiKey"];
        if (string.IsNullOrWhiteSpace(configured))
            return StatusCode(503, new { error = "Content approval callback is disabled (configure ContentApproval:ApiKey)." });

        if (!Request.Headers.TryGetValue(HeaderName, out var header) || header.Count == 0)
            return Unauthorized(new { error = "X-Internal-Api-Key header is required." });

        var a = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(header.ToString()));
        return CryptographicOperations.FixedTimeEquals(a, b) ? null : Forbid();
    }
}
