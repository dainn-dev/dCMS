using System.Security.Cryptography;
using System.Text;
using dCMS.Provisioning.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

[ApiController]
[Route("umbraco/dcms/api")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class TenantIntegrationController : ControllerBase
{
    private static readonly HashSet<string> PlatformAdminGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcmsItAdministrator",
        "dcmsSysAdministrator",
    };

    private readonly IIntegrationAppRepository _apps;
    private readonly ITenantIntegrationRepository _integrations;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;

    public TenantIntegrationController(
        IIntegrationAppRepository apps,
        ITenantIntegrationRepository integrations,
        IBackOfficeSecurityAccessor securityAccessor)
    {
        _apps = apps;
        _integrations = integrations;
        _securityAccessor = securityAccessor;
    }

    [HttpGet("integration-apps")]
    public async Task<IActionResult> ListApps(CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var rows = await _apps.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { data = rows.Select(a => new { id = a.Id, name = a.Name, scopes = a.Scopes, eventTypes = a.EventTypes }), meta = (object?)null, error = (object?)null });
    }

    [HttpGet("tenants/{tenantId}/integrations")]
    public async Task<IActionResult> ListTenantIntegrations(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var rows = await _integrations.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            data = rows.Select(r => new { id = r.Id, appId = r.AppId, clientId = r.ClientId, status = r.Status.ToString().ToLowerInvariant() }),
            meta = (object?)null,
            error = (object?)null,
        });
    }

    [HttpPost("tenants/{tenantId}/integrations")]
    public async Task<IActionResult> CreateIntegration(
        string tenantId,
        [FromBody] CreateIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var app = await _apps.GetByIdAsync(request.AppId, cancellationToken).ConfigureAwait(false);
        if (app is null)
            return BadRequest(ErrorEnvelope("Integration app not found."));

        var clientId = $"cli_{Guid.NewGuid():N}";
        var clientSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var record = new TenantIntegrationRecord(
            $"int_{Guid.NewGuid():N}",
            tenantId,
            app.Id,
            clientId,
            HashSecret(clientSecret),
            TenantIntegrationStatus.Active,
            now,
            now);

        await _integrations.CreateAsync(record, cancellationToken).ConfigureAwait(false);
        return Created($"/umbraco/dcms/api/tenants/{tenantId}/integrations/{record.Id}",
            new { data = new { id = record.Id, clientId, clientSecret, appId = app.Id }, meta = (object?)null, error = (object?)null });
    }

    [HttpPost("tenants/{tenantId}/integrations/{integrationId}/revoke")]
    public async Task<IActionResult> Revoke(string tenantId, string integrationId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        await _integrations.RevokeAsync(integrationId, cancellationToken).ConfigureAwait(false);
        return Ok(new { data = new { id = integrationId, status = "revoked" }, meta = (object?)null, error = (object?)null });
    }

    private static string HashSecret(string secret)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private IActionResult? DenyIfNotPlatformAdmin()
    {
        var user = _securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
            return Unauthorized(ErrorEnvelope("Authentication required."));
        if (!user.Groups.Any(g => PlatformAdminGroups.Contains(g.Alias)))
            return StatusCode(403, ErrorEnvelope("Platform administrator access required."));
        return null;
    }

    private static object ErrorEnvelope(string message) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "error", message },
    };

    public sealed class CreateIntegrationRequest
    {
        public string AppId { get; set; } = "";
    }
}
