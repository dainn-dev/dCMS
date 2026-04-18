using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Web.Controllers;

/// <summary>
/// Backoffice API for managing Siêu thị (Tenants) — dCMS Access §8.
///
/// TODO: Wire GET / to Catalog PostgreSQL DB (tenant table) in a follow-up task.
///       POST / PUT / DELETE will be fully implemented then.
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/tenants")]
[Authorize(Policy = "BackOfficeAccess")]
public sealed class DcmsTenantController : ControllerBase
{
    // ── GET / ────────────────────────────────────────────────────────────────
    /// <summary>Returns a stub list of tenants.
    /// TODO: Replace with real Catalog DB query in follow-up task.</summary>
    [HttpGet]
    public IActionResult List()
    {
        var stub = new[]
        {
            new { id = "tenant-001", code = "DEMO01", name = "Demo Siêu Thị 1", contactName = "Admin",    contactEmail = "admin@demo1.vn" },
            new { id = "tenant-002", code = "DEMO02", name = "Demo Siêu Thị 2", contactName = "Manager",  contactEmail = "mgr@demo2.vn"   },
        };

        return Ok(new
        {
            data = stub,
            meta = new { note = "Stub data — Tenant management will be wired to Catalog DB in a follow-up task." },
            error = (object?)null,
        });
    }

    // ── POST / ───────────────────────────────────────────────────────────────
    [HttpPost]
    public IActionResult Create([FromBody] object _) =>
        StatusCode(501, NotImplementedEnvelope());

    // ── PUT /{id} ─────────────────────────────────────────────────────────────
    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] object _) =>
        StatusCode(501, NotImplementedEnvelope());

    // ── DELETE /{id} ──────────────────────────────────────────────────────────
    [HttpDelete("{id}")]
    public IActionResult Delete(string id) =>
        StatusCode(501, NotImplementedEnvelope());

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object NotImplementedEnvelope() => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new
        {
            code = "NOT_IMPLEMENTED",
            message = "Tenant management will be wired to Catalog DB in a follow-up task.",
        },
    };
}
