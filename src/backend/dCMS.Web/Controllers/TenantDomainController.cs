using System.Text.Json;
using dCMS.Infrastructure.Platform;
using dCMS.Provisioning.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

[ApiController]
[Route("umbraco/dcms/api/tenants/{tenantId}/domains")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class TenantDomainController : ControllerBase
{
    private static readonly HashSet<string> PlatformAdminGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcmsItAdministrator",
        "dcmsSysAdministrator",
    };

    private readonly ITenantProvisioningRepository _provisioning;
    private readonly IDomainBindingRedisSync _redisSync;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;

    public TenantDomainController(
        ITenantProvisioningRepository provisioning,
        IDomainBindingRedisSync redisSync,
        IBackOfficeSecurityAccessor securityAccessor)
    {
        _provisioning = provisioning;
        _redisSync = redisSync;
        _securityAccessor = securityAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> List(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var bindings = await _provisioning.ListDomainBindingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new { data = bindings.Select(Map), meta = (object?)null, error = (object?)null });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string tenantId,
        [FromBody] CreateDomainRequest request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        if (string.IsNullOrWhiteSpace(request.Domain) || string.IsNullOrWhiteSpace(request.StoreId))
            return BadRequest(ErrorEnvelope("Domain and StoreId are required."));

        var domain = request.Domain.Trim().ToLowerInvariant();
        var redisKey = $"dcms:host:{domain}";
        var status = request.Activate ? DomainBindingStatus.Active : DomainBindingStatus.Pending;

        if (status == DomainBindingStatus.Active)
            await _redisSync.SyncActiveBindingAsync(domain, tenantId, request.StoreId.Trim(), cancellationToken)
                .ConfigureAwait(false);

        await _provisioning.UpsertDomainBindingAsync(
            domain, tenantId, request.StoreId.Trim(), request.IsPrimary, status, redisKey,
            JsonSerializer.Serialize(new[] { redisKey }), cancellationToken).ConfigureAwait(false);

        var bindings = await _provisioning.ListDomainBindingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var created = bindings.First(b => b.Domain == domain);
        return Created($"/umbraco/dcms/api/tenants/{tenantId}/domains/{domain}",
            new { data = Map(created), meta = (object?)null, error = (object?)null });
    }

    [HttpPost("{domain}/activate")]
    public async Task<IActionResult> Activate(string tenantId, string domain, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var bindings = await _provisioning.ListDomainBindingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var binding = bindings.FirstOrDefault(b => b.Domain == domain.ToLowerInvariant());
        if (binding is null)
            return NotFound(ErrorEnvelope("Domain binding not found."));

        var redisKey = $"dcms:host:{binding.Domain}";
        await _redisSync.SyncActiveBindingAsync(binding.Domain, tenantId, binding.StoreId, cancellationToken)
            .ConfigureAwait(false);
        await _provisioning.UpsertDomainBindingAsync(
            binding.Domain, tenantId, binding.StoreId, binding.IsPrimary, DomainBindingStatus.Active,
            redisKey, JsonSerializer.Serialize(new[] { redisKey }), cancellationToken).ConfigureAwait(false);

        return Ok(new { data = new { domain = binding.Domain, status = "active" }, meta = (object?)null, error = (object?)null });
    }

    [HttpDelete("{domain}")]
    public async Task<IActionResult> Remove(string tenantId, string domain, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var normalized = domain.ToLowerInvariant();
        var bindings = await _provisioning.ListDomainBindingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var binding = bindings.FirstOrDefault(b => b.Domain == normalized);
        if (binding is null)
            return NotFound(ErrorEnvelope("Domain binding not found."));

        await _redisSync.RemoveBindingAsync(normalized, cancellationToken).ConfigureAwait(false);
        await _provisioning.UpsertDomainBindingAsync(
            normalized, tenantId, binding.StoreId, binding.IsPrimary, DomainBindingStatus.Removed,
            null, "[]", cancellationToken).ConfigureAwait(false);

        return Ok(new { data = new { domain = normalized, status = "removed" }, meta = (object?)null, error = (object?)null });
    }

    private static object Map(TenantDomainBindingRecord b) => new
    {
        domain = b.Domain,
        tenantId = b.TenantId,
        storeId = b.StoreId,
        isPrimary = b.IsPrimary,
        status = b.Status.ToDbString(),
        redisHostKey = b.RedisHostKey,
        createdAt = b.CreatedAt,
        updatedAt = b.UpdatedAt,
        activatedAt = b.ActivatedAt,
    };

    private IActionResult? DenyIfNotPlatformAdmin()
    {
        var user = _securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
            return Unauthorized(ErrorEnvelope("Authentication required."));
        if (!user.Groups.Any(g => PlatformAdminGroups.Contains(g.Alias)))
            return StatusCode(403, ErrorEnvelope("Platform administrator access required."));
        return null;
    }

    private static object ErrorEnvelope(string message, Exception? ex = null) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "error", message, detail = ex?.Message },
    };

    public sealed class CreateDomainRequest
    {
        public string Domain { get; set; } = "";
        public string StoreId { get; set; } = "default";
        public bool IsPrimary { get; set; }
        public bool Activate { get; set; }
    }
}
