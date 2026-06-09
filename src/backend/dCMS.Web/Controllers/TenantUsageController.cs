using dCMS.Billing.Domain;
using dCMS.Provisioning.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

[ApiController]
[Route("umbraco/dcms/api/tenants/{tenantId}")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class TenantUsageController : ControllerBase
{
    private static readonly HashSet<string> PlatformAdminGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcmsItAdministrator",
        "dcmsSysAdministrator",
    };

    private readonly ITenantUsageRepository _usage;
    private readonly ITenantEntitlementRepository _entitlements;
    private readonly ITenantFeatureOverrideRepository _overrides;
    private readonly ITenantEntitlementPublisher _publisher;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;

    public TenantUsageController(
        ITenantUsageRepository usage,
        ITenantEntitlementRepository entitlements,
        ITenantFeatureOverrideRepository overrides,
        ITenantEntitlementPublisher publisher,
        IBackOfficeSecurityAccessor securityAccessor)
    {
        _usage = usage;
        _entitlements = entitlements;
        _overrides = overrides;
        _publisher = publisher;
        _securityAccessor = securityAccessor;
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var record = await _entitlements.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (record is null)
            return NotFound(ErrorEnvelope("Tenant subscription not found."));

        var today = await _usage.GetTodayAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var limits = new
        {
            maxBrands = record.MaxBrands,
            maxActiveProducts = record.MaxActiveProducts,
        };
        var usage = new
        {
            ordersToday = today.OrdersCount,
            apiCallsToday = today.ApiCallsCount,
            webhookDeliveriesToday = today.WebhookDeliveriesCount,
            activeProducts = today.ActiveProductsCount,
        };
        var warnings = BuildWarnings(record.MaxActiveProducts, today.ActiveProductsCount);

        return Ok(new
        {
            data = new { limits, usage, warnings },
            meta = new { asOf = today.UpdatedAt },
            error = (object?)null,
        });
    }

    [HttpGet("feature-overrides")]
    public async Task<IActionResult> ListFeatureOverrides(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var rows = await _overrides.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new { data = rows.Select(r => new { feature = r.Feature, enabled = r.Enabled }), meta = (object?)null, error = (object?)null });
    }

    [HttpPut("feature-overrides/{feature}")]
    public async Task<IActionResult> UpsertFeatureOverride(
        string tenantId,
        string feature,
        [FromBody] FeatureOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        await _overrides.UpsertAsync(tenantId, feature, request.Enabled, cancellationToken).ConfigureAwait(false);
        await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new { data = new { feature, enabled = request.Enabled }, meta = (object?)null, error = (object?)null });
    }

    [HttpDelete("feature-overrides/{feature}")]
    public async Task<IActionResult> DeleteFeatureOverride(string tenantId, string feature, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        await _overrides.DeleteAsync(tenantId, feature, cancellationToken).ConfigureAwait(false);
        await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new { data = new { feature, removed = true }, meta = (object?)null, error = (object?)null });
    }

    private static IReadOnlyList<object> BuildWarnings(int maxProducts, long activeProducts)
    {
        var list = new List<object>();
        if (maxProducts > 0 && activeProducts >= maxProducts * 0.8)
            list.Add(new { code = "quota_warning", quota = "max_active_products", percent = (double)activeProducts / maxProducts });
        return list;
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

    public sealed class FeatureOverrideRequest
    {
        public bool Enabled { get; set; }
    }
}
