using dCMS.Billing.Domain;
using dCMS.Web.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

/// <summary>Super Admin billing/subscription management (DAI-29 manual invoicing MVP).</summary>
[ApiController]
[Route("umbraco/dcms/api/tenants/{tenantId}/subscription")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class TenantSubscriptionController : ControllerBase
{
    private static readonly HashSet<string> PlatformAdminGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcmsItAdministrator",
        "dcmsSysAdministrator",
    };

    private readonly ITenantEntitlementRepository _repository;
    private readonly ITenantEntitlementPublisher _publisher;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;

    public TenantSubscriptionController(
        ITenantEntitlementRepository repository,
        ITenantEntitlementPublisher publisher,
        IBackOfficeSecurityAccessor securityAccessor)
    {
        _repository = repository;
        _publisher = publisher;
        _securityAccessor = securityAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (record is null)
                return NotFound(ErrorEnvelope("Tenant subscription not found."));

            return Ok(new { data = Map(record), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to load tenant subscription.", ex));
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateInvoice(
        string tenantId,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            var status = request.ManualInvoiceStatus is null
                ? ManualInvoiceStatus.None
                : ManualInvoiceStatusExtensions.ParsePersisted(request.ManualInvoiceStatus);

            await _repository.UpdateManualInvoiceAsync(
                tenantId,
                status,
                request.InvoiceReference,
                request.InvoiceNotes,
                cancellationToken).ConfigureAwait(false);
            await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);

            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(new { data = Map(record!), meta = (object?)null, error = (object?)null });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update invoice metadata.", ex));
        }
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            await _repository.ActivateAsync(tenantId, cancellationToken).ConfigureAwait(false);
            await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(new { data = Map(record!), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to activate subscription.", ex));
        }
    }

    [HttpPost("suspend")]
    public async Task<IActionResult> Suspend(
        string tenantId,
        [FromBody] ReasonRequest? request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            await _repository.SuspendAsync(tenantId, request?.Reason, cancellationToken).ConfigureAwait(false);
            await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(new { data = Map(record!), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to suspend subscription.", ex));
        }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(
        string tenantId,
        [FromBody] ReasonRequest? request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            await _repository.CancelAsync(tenantId, request?.Reason, cancellationToken).ConfigureAwait(false);
            await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(new { data = Map(record!), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to cancel subscription.", ex));
        }
    }

    [HttpPut("plan")]
    public async Task<IActionResult> ChangePlan(
        string tenantId,
        [FromBody] ChangePlanRequest request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        if (string.IsNullOrWhiteSpace(request.PlanId))
            return BadRequest(ErrorEnvelope("planId is required."));

        try
        {
            await _repository.ChangePlanAsync(tenantId, request.PlanId.Trim(), request.PendingPlanId, cancellationToken)
                .ConfigureAwait(false);
            await _publisher.PublishFromRepositoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(new { data = Map(record!), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to change plan.", ex));
        }
    }

    private IActionResult? DenyIfNotPlatformAdmin()
    {
        var user = _securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
            return Unauthorized(ErrorEnvelope("Not authenticated."));

        if (!IsPlatformAdmin(user))
            return StatusCode(StatusCodes.Status403Forbidden, ErrorEnvelope("Platform administrator access required."));

        return null;
    }

    private static bool IsPlatformAdmin(IUser user) =>
        user.Groups.Any(g => PlatformAdminGroups.Contains(g.Alias));

    private static object Map(TenantSubscriptionRecord record) => new
    {
        tenantId = record.TenantId,
        planId = record.PlanId,
        planCode = record.PlanCode.ToPersistedValue(),
        planName = record.PlanName,
        subscriptionState = record.SubscriptionState.ToPersistedValue(),
        manualInvoiceStatus = record.ManualInvoiceStatus.ToPersistedValue(),
        tenantActive = record.TenantActive,
        trialEndsAt = record.TrialEndsAt,
        currentPeriodStart = record.CurrentPeriodStart,
        currentPeriodEnd = record.CurrentPeriodEnd,
        pendingPlanId = record.PendingPlanId,
        suspendedAt = record.SuspendedAt,
        cancelledAt = record.CancelledAt,
        cancellationReason = record.CancellationReason,
        invoiceReference = record.InvoiceReference,
        invoiceNotes = record.InvoiceNotes,
        maxBrands = record.MaxBrands,
        maxActiveProducts = record.MaxActiveProducts,
        features = record.Features,
        updatedAt = record.UpdatedAt,
    };

    private static object ErrorEnvelope(string message, Exception? _ = null) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "ERROR", message },
    };

    public sealed class UpdateInvoiceRequest
    {
        public string? ManualInvoiceStatus { get; set; }
        public string? InvoiceReference { get; set; }
        public string? InvoiceNotes { get; set; }
    }

    public sealed class ReasonRequest
    {
        public string? Reason { get; set; }
    }

    public sealed class ChangePlanRequest
    {
        public string? PlanId { get; set; }
        public string? PendingPlanId { get; set; }
    }
}
