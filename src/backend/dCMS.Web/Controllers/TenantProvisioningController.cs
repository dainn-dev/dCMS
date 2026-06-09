using dCMS.Provisioning.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

/// <summary>Read-only Super Admin API for tenant provisioning lifecycle (DAI-29).</summary>
[ApiController]
[Route("umbraco/dcms/api/tenants/{tenantId}/provisioning")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class TenantProvisioningController : ControllerBase
{
    private static readonly HashSet<string> PlatformAdminGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcmsItAdministrator",
        "dcmsSysAdministrator",
    };

    private readonly ITenantProvisioningRepository _repository;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;

    public TenantProvisioningController(
        ITenantProvisioningRepository repository,
        IBackOfficeSecurityAccessor securityAccessor)
    {
        _repository = repository;
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
                return NotFound(ErrorEnvelope("Provisioning record not found."));

            return Ok(new { data = Map(record), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to load provisioning status.", ex));
        }
    }

    [HttpGet("steps")]
    public async Task<IActionResult> GetSteps(
        string tenantId,
        [FromQuery] Guid? runId,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (record is null)
                return NotFound(ErrorEnvelope("Provisioning record not found."));

            var effectiveRunId = runId ?? record.CurrentRunId;
            if (effectiveRunId is null)
                return Ok(new { data = Array.Empty<object>(), meta = (object?)null, error = (object?)null });

            var steps = await _repository.GetStepsAsync(tenantId, effectiveRunId.Value, cancellationToken)
                .ConfigureAwait(false);

            var data = steps.Select(s => new
            {
                id = s.Id,
                runId = s.RunId,
                stepOrder = s.StepOrder,
                stepName = s.StepName,
                status = s.Status.ToDbString(),
                attemptCount = s.AttemptCount,
                maxRetries = s.MaxRetries,
                errorMessage = s.ErrorMessage,
                lastAttemptAt = s.LastAttemptAt,
                rollbackStatus = s.RollbackStatus?.ToDbString(),
                completedAt = s.CompletedAt,
            });

            return Ok(new { data, meta = new { runId = effectiveRunId }, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to load provisioning steps.", ex));
        }
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit(
        string tenantId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            var entries = await _repository.ListAuditAsync(tenantId, limit, cancellationToken).ConfigureAwait(false);
            var data = entries.Select(e => new
            {
                id = e.Id,
                runId = e.RunId,
                operation = e.Operation,
                fromStatus = e.FromStatus,
                toStatus = e.ToStatus,
                actor = e.Actor,
                details = e.DetailsJson,
                createdAt = e.CreatedAt,
            });
            return Ok(new { data, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to load provisioning audit.", ex));
        }
    }

    [HttpGet("onboarding")]
    public async Task<IActionResult> GetOnboarding(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        try
        {
            var items = await _repository.ListOnboardingAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var data = items.Select(i => new
            {
                id = i.Id,
                checkItem = i.CheckItem,
                status = i.Status.ToDbString(),
                isRequired = i.IsRequired,
                completedAt = i.CompletedAt,
                verifiedAt = i.VerifiedAt,
                verifiedBy = i.VerifiedBy,
                notes = i.Notes,
            });
            return Ok(new { data, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to load onboarding checklist.", ex));
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

    private static object Map(TenantProvisioningRecord record) => new
    {
        tenantId = record.TenantId,
        tenantCode = record.TenantCode,
        status = record.Status.ToApiString(),
        internalStatus = record.Status.ToDbString(),
        planTier = record.PlanTier,
        umbracoDbName = record.UmbracoDbName,
        envFilePath = record.EnvFilePath,
        primaryDomain = record.PrimaryDomain,
        currentRunId = record.CurrentRunId,
        lastSuccessfulRunId = record.LastSuccessfulRunId,
        onboardingComplete = record.OnboardingComplete,
        onboardingCompletedAt = record.OnboardingCompletedAt,
        requestedAt = record.RequestedAt,
        requestedBy = record.RequestedBy,
        provisioningStartedAt = record.ProvisioningStartedAt,
        provisionedAt = record.ProvisionedAt,
        suspendedAt = record.SuspendedAt,
        deprovisionedAt = record.DeprovisionedAt,
        updatedAt = record.UpdatedAt,
        lastFailureMessage = record.LastFailureMessage,
        failureCount = record.FailureCount,
    };

    private static object ErrorEnvelope(string message, Exception? _ = null) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "ERROR", message },
    };
}
