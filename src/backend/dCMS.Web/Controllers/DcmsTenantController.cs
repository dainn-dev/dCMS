using System.Text.RegularExpressions;
using dCMS.Billing.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Persistence;
using dCMS.Web.Billing;

namespace dCMS.Web.Controllers;

/// <summary>
/// Backoffice API for managing Siêu thị (Tenants) — dCMS Access §8 (DAI-668: <c>dcms_tenants</c>).
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/tenants")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class DcmsTenantController : ControllerBase
{
    private static readonly Regex CodeRegex = new(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,18}$", RegexOptions.Compiled);

    private readonly IUmbracoDatabaseFactory _dbFactory;
    private readonly ITenantEntitlementRepository _entitlementRepository;
    private readonly ITenantEntitlementPublisher _entitlementPublisher;
    private readonly IConfiguration _configuration;

    public DcmsTenantController(
        IUmbracoDatabaseFactory dbFactory,
        ITenantEntitlementRepository entitlementRepository,
        ITenantEntitlementPublisher entitlementPublisher,
        IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _entitlementRepository = entitlementRepository;
        _entitlementPublisher = entitlementPublisher;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            using var db = _dbFactory.CreateDatabase();
            var rows = await db.FetchAsync<DcmsTenantRow>("""
                SELECT id AS Id, code AS Code, name AS Name,
                       contact_name AS ContactName, contact_email AS ContactEmail,
                       brand_count AS BrandCount, active AS Active,
                       provisioning_status AS ProvisioningStatus, plan_tier AS PlanTier,
                       provisioning_run_id AS ProvisioningRunId
                FROM dcms_tenants
                ORDER BY name
                """).ConfigureAwait(false);

            var data = rows.Select(r => new
            {
                id = r.Id,
                code = r.Code,
                name = r.Name,
                contactName = r.ContactName,
                contactEmail = r.ContactEmail,
                brandCount = r.BrandCount,
                active = r.Active,
                provisioningStatus = r.ProvisioningStatus,
                planTier = r.PlanTier,
                provisioningRunId = r.ProvisioningRunId,
            });

            return Ok(new { data, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to load tenants.", ex));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTenantRequest request)
    {
        if (ValidateUpsert(request) is { } createErr)
            return BadRequest(ErrorEnvelope(createErr));

        var id = Guid.NewGuid().ToString("N");
        var code = request.Code!.Trim();
        var name = request.Name!.Trim();
        var contactName = (request.ContactName ?? "").Trim();
        var contactEmail = (request.ContactEmail ?? "").Trim();
        var brandCount = request.BrandCount ?? 0;

        try
        {
            using var db = _dbFactory.CreateDatabase();
            var n = await db.ExecuteAsync("""
                INSERT INTO dcms_tenants (id, code, name, contact_name, contact_email, brand_count, active, created_at, updated_at)
                VALUES (@0, @1, @2, @3, @4, @5, 1, GETUTCDATE(), GETUTCDATE())
                """, id, code, name, contactName, contactEmail, brandCount).ConfigureAwait(false);

            if (n != 1)
                return StatusCode(500, ErrorEnvelope("Insert failed."));

            var trialDays = _configuration.GetValue("Dcms:Billing:DefaultTrialDays", 14);
            var defaultPlan = PlanCode.Starter;
            if (Enum.TryParse<PlanCode>(_configuration["Dcms:Billing:DefaultPlan"], true, out var configuredPlan))
                defaultPlan = configuredPlan;

            await _entitlementRepository.CreateDefaultTrialSubscriptionAsync(id, defaultPlan, trialDays)
                .ConfigureAwait(false);
            await _entitlementPublisher.PublishFromRepositoryAsync(id).ConfigureAwait(false);

            return Ok(new
            {
                data = new { id, code, name, contactName, contactEmail, brandCount, active = true },
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return Conflict(ErrorEnvelope($"Tenant code '{code}' already exists."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to create tenant.", ex));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpsertTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(ErrorEnvelope("Id is required."));

        if (ValidateUpsert(request) is { } updateErr)
            return BadRequest(ErrorEnvelope(updateErr));

        var code = request.Code!.Trim();
        var name = request.Name!.Trim();
        var contactName = (request.ContactName ?? "").Trim();
        var contactEmail = (request.ContactEmail ?? "").Trim();
        var brandCount = request.BrandCount ?? 0;
        var active = request.Active ?? true;

        try
        {
            using var db = _dbFactory.CreateDatabase();
            var n = await db.ExecuteAsync("""
                UPDATE dcms_tenants
                SET code = @0, name = @1, contact_name = @2, contact_email = @3,
                    brand_count = @4, active = @5, updated_at = GETUTCDATE()
                WHERE id = @6
                """, code, name, contactName, contactEmail, brandCount, active ? 1 : 0, id).ConfigureAwait(false);

            if (n == 0)
                return NotFound(ErrorEnvelope("Tenant not found."));

            await _entitlementPublisher.PublishFromRepositoryAsync(id).ConfigureAwait(false);

            return Ok(new
            {
                data = new { id, code, name, contactName, contactEmail, brandCount, active },
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return Conflict(ErrorEnvelope($"Tenant code '{code}' already exists."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update tenant.", ex));
        }
    }

    /// <summary>Soft-deactivate tenant (<c>active = 0</c>).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(ErrorEnvelope("Id is required."));

        try
        {
            using var db = _dbFactory.CreateDatabase();
            var n = await db.ExecuteAsync("""
                UPDATE dcms_tenants SET active = 0, updated_at = GETUTCDATE() WHERE id = @0
                """, id).ConfigureAwait(false);

            if (n == 0)
                return NotFound(ErrorEnvelope("Tenant not found."));

            await _entitlementPublisher.PublishFromRepositoryAsync(id).ConfigureAwait(false);

            return Ok(new { data = new { id, deleted = true, active = false }, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to archive tenant.", ex));
        }
    }

    private static string? ValidateUpsert(UpsertTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return "Code is required.";
        if (!CodeRegex.IsMatch(request.Code.Trim()))
            return "Code must be 1–20 characters: letters, digits, underscore or hyphen.";
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (request.Name.Trim().Length > 256)
            return "Name is too long (max 256).";
        if ((request.ContactName ?? "").Length > 128)
            return "Contact name is too long (max 128).";
        if ((request.ContactEmail ?? "").Length > 256)
            return "Contact email is too long (max 256).";

        return null;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        if (ex is Microsoft.Data.SqlClient.SqlException sql && (sql.Number is 2601 or 2627))
            return true;
        if (ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("uq_dcms_tenants_code", StringComparison.OrdinalIgnoreCase))
            return true;
        return ex.InnerException != null && IsUniqueViolation(ex.InnerException);
    }

    private static object ErrorEnvelope(string message, Exception? _ = null) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "ERROR", message },
    };

    public sealed class UpsertTenantRequest
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public int? BrandCount { get; set; }
        public bool? Active { get; set; }
    }

    private sealed class DcmsTenantRow
    {
        public string Id { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string ContactName { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public int BrandCount { get; set; }
        public bool Active { get; set; }
        public string ProvisioningStatus { get; set; } = "requested";
        public string PlanTier { get; set; } = "starter";
        public string? ProvisioningRunId { get; set; }
    }
}
