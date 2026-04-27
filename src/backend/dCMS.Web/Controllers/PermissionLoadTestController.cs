using System.Diagnostics;
using dCMS.Web.Access.Caching;
using dCMS.Web.Access.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace dCMS.Web.Controllers;

/// <summary>
/// DAI-705 — internal-only endpoint to load test PermissionService caching.
/// Enabled only in Development and guarded by a shared key.
/// </summary>
[ApiController]
[Route("dcms-internal/perm-loadtest")]
public sealed class PermissionLoadTestController : ControllerBase
{
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IPermissionService _permissionService;
    private readonly IPermissionCache _permissionCache;

    public PermissionLoadTestController(
        IHostEnvironment env,
        IConfiguration configuration,
        IPermissionService permissionService,
        IPermissionCache permissionCache)
    {
        _env = env;
        _configuration = configuration;
        _permissionService = permissionService;
        _permissionCache = permissionCache;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] int userId,
        [FromQuery] string module,
        [FromQuery] string action,
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var expected = _configuration["Dcms:Access:LoadTestKey"]?.Trim() ?? "";
        var provided = Request.Headers["X-LoadTest-Key"].FirstOrDefault()?.Trim() ?? "";
        if (expected.Length == 0 || !string.Equals(expected, provided, StringComparison.Ordinal))
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Missing or invalid X-LoadTest-Key." } });

        if (userId <= 0)
            return BadRequest(new { error = new { code = "INVALID_USER_ID", message = "userId must be positive." } });
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(action))
            return BadRequest(new { error = new { code = "INVALID_ARGS", message = "module and action are required." } });

        // For load testing we keep the tenant stable; Umbraco instances are tenant-isolated by deployment anyway.
        var tenantId = _configuration.GetSection("Dcms:Estore")["TenantId"]?.Trim();
        tenantId = string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId;

        // We cannot read the rolesHash from PermissionService directly, so approximate by asking for grants once.
        // The first call warms cache; subsequent calls should be hits.
        var sw = Stopwatch.StartNew();
        var allowed = await _permissionService.HasPermissionAsync(userId, module, action, ct).ConfigureAwait(false);
        sw.Stop();

        // A second call is expected to be a cache hit (same user/module/roles).
        var sw2 = Stopwatch.StartNew();
        var allowed2 = await _permissionService.HasPermissionAsync(userId, module, action, ct).ConfigureAwait(false);
        sw2.Stop();

        return Ok(new
        {
            tenantId,
            userId,
            module,
            action,
            allowed,
            allowed2,
            firstMs = sw.Elapsed.TotalMilliseconds,
            secondMs = sw2.Elapsed.TotalMilliseconds,
        });
    }
}

