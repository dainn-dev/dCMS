using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Controllers;

/// <summary>Backoffice API for managing Umbraco UserGroups and their module permissions (dCMS Access §8).</summary>
[ApiController]
[Route("umbraco/dcms/api/roles")]
[Authorize(Policy = "BackOfficeAccess")]
public sealed class DcmsRoleController : ControllerBase
{
    private readonly IUserGroupService _userGroupService;
    private readonly IUserService _userService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IUmbracoDatabaseFactory _dbFactory;

    public DcmsRoleController(
        IUserGroupService userGroupService,
        IUserService userService,
        IShortStringHelper shortStringHelper,
        IUmbracoDatabaseFactory dbFactory)
    {
        _userGroupService = userGroupService;
        _userService = userService;
        _shortStringHelper = shortStringHelper;
        _dbFactory = dbFactory;
    }

    // ── GET / ────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            var page = await _userGroupService.GetAllAsync(0, 1000).ConfigureAwait(false);
            var groups = page.Items.ToList();

            using var db = _dbFactory.CreateDatabase();

            var countRows = await db.FetchAsync<GroupMemberCountRow>("""
                SELECT userGroupId AS UserGroupId, COUNT(*) AS MemberCount
                FROM umbracoUser2UserGroup
                GROUP BY userGroupId
                """).ConfigureAwait(false);
            var counts = countRows.ToDictionary(c => c.UserGroupId, c => c.MemberCount);

            var metaRows = await db.FetchAsync<RoleMetaRow>("""
                SELECT role_alias AS RoleAlias, is_tenant_role AS IsTenantRole, description AS Description
                FROM dcms_roles_meta
                """).ConfigureAwait(false);
            var meta = metaRows.ToDictionary(m => m.RoleAlias, m => m, StringComparer.OrdinalIgnoreCase);

            var items = groups.Select(g =>
            {
                counts.TryGetValue(g.Id, out var memberCount);
                meta.TryGetValue(g.Alias, out var m);
                return MapGroupRich(g, memberCount, m?.IsTenantRole ?? false, m?.Description ?? "");
            });

            return Ok(new { data = items, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to retrieve roles.", ex));
        }
    }

    // ── POST / ───────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ErrorEnvelope("Name is required."));

        if (string.IsNullOrWhiteSpace(request.Alias))
            return BadRequest(ErrorEnvelope("Alias is required."));

        try
        {
            var group = new UserGroup(_shortStringHelper)
            {
                Name = request.Name,
                Alias = request.Alias,
                Icon = request.Icon ?? "icon-users",
            };

            await _userGroupService.CreateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);

            using (var db = _dbFactory.CreateDatabase())
            {
                await UpsertRoleMetaAsync(db, group.Alias, request.IsTenantRole ?? false, request.Description ?? string.Empty)
                    .ConfigureAwait(false);
            }

            return Ok(new
            {
                data = MapGroupRich(group, 0, request.IsTenantRole ?? false, request.Description ?? ""),
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to create role.", ex));
        }
    }

    // ── PUT /{alias} ──────────────────────────────────────────────────────────
    [HttpPut("{alias}")]
    public async Task<IActionResult> Update(string alias, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var group = await _userGroupService.GetAsync(alias).ConfigureAwait(false);
            if (group is null)
                return NotFound(ErrorEnvelope("Role not found."));

            if (!string.IsNullOrWhiteSpace(request.Name))
                group.Name = request.Name;

            if (!string.IsNullOrWhiteSpace(request.Icon))
                group.Icon = request.Icon;

            await _userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);

            using (var db = _dbFactory.CreateDatabase())
            {
                var existingMeta = await db.FirstOrDefaultAsync<RoleMetaRow>("""
                    SELECT role_alias AS RoleAlias, is_tenant_role AS IsTenantRole, description AS Description
                    FROM dcms_roles_meta
                    WHERE role_alias = @0
                    """, alias).ConfigureAwait(false);

                var isTenant = request.IsTenantRole ?? existingMeta?.IsTenantRole ?? false;
                var description = request.Description ?? existingMeta?.Description ?? string.Empty;
                await UpsertRoleMetaAsync(db, group.Alias, isTenant, description).ConfigureAwait(false);

                var memberCount = await db.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM umbracoUser2UserGroup WHERE userGroupId = @0
                    """, group.Id).ConfigureAwait(false);

                return Ok(new
                {
                    data = MapGroupRich(group, memberCount, isTenant, description),
                    meta = (object?)null,
                    error = (object?)null,
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update role.", ex));
        }
    }

    // ── DELETE /{alias} ───────────────────────────────────────────────────────
    [HttpDelete("{alias}")]
    public async Task<IActionResult> Delete(string alias)
    {
        try
        {
            var group = await _userGroupService.GetAsync(alias).ConfigureAwait(false);
            if (group is null)
                return NotFound(ErrorEnvelope("Role not found."));

            // Return 409 if the group has members
            var members = _userService.GetAllInGroup(group.Id);
            if (members.Any())
                return Conflict(ErrorEnvelope("Cannot delete a role that still has members."));

            await _userGroupService.DeleteAsync(new HashSet<Guid> { group.Key }).ConfigureAwait(false);

            using (var db = _dbFactory.CreateDatabase())
            {
                await db.ExecuteAsync("DELETE FROM dcms_roles_meta WHERE role_alias = @0", alias).ConfigureAwait(false);
            }

            return Ok(new { data = new { deleted = true }, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to delete role.", ex));
        }
    }

    // ── GET /{alias}/permissions ───────────────────────────────────────────────
    [HttpGet("{alias}/permissions")]
    public async Task<IActionResult> GetPermissions(string alias)
    {
        try
        {
            using var db = _dbFactory.CreateDatabase();
            var rows = await db.FetchAsync<PermissionRow>(
                "SELECT role_alias, module, action, granted FROM dcms_role_module_permissions WHERE role_alias = @0",
                alias).ConfigureAwait(false);

            return Ok(new { data = rows, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to retrieve permissions.", ex));
        }
    }

    // ── PUT /{alias}/permissions/{module} ─────────────────────────────────────
    [HttpPut("{alias}/permissions/{module}")]
    public async Task<IActionResult> UpsertModulePermissions(
        string alias,
        string module,
        [FromBody] UpsertPermissionsRequest request)
    {
        if (request.Actions is null || request.Actions.Count == 0)
            return BadRequest(ErrorEnvelope("Actions list is required."));

        try
        {
            using var db = _dbFactory.CreateDatabase();

            foreach (var entry in request.Actions)
            {
                if (string.IsNullOrWhiteSpace(entry.Action))
                    continue;

                await db.ExecuteAsync("""
                    MERGE dcms_role_module_permissions AS target
                    USING (VALUES (@0, @1, @2, @3)) AS src (role_alias, module, action, granted)
                        ON target.role_alias = src.role_alias AND target.module = src.module AND target.action = src.action
                    WHEN MATCHED THEN
                        UPDATE SET granted = src.granted
                    WHEN NOT MATCHED THEN
                        INSERT (role_alias, module, action, granted)
                        VALUES (src.role_alias, src.module, src.action, src.granted);
                    """,
                    alias, module, entry.Action, entry.Granted ? 1 : 0).ConfigureAwait(false);
            }

            return Ok(new { data = new { updated = request.Actions.Count }, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to upsert module permissions.", ex));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object MapGroupRich(IUserGroup g, int memberCount, bool isTenantRole, string description) => new
    {
        alias = g.Alias,
        name = g.Name,
        icon = g.Icon,
        allowedSections = g.AllowedSections.ToArray(),
        memberCount,
        isTenantRole,
        description,
    };

    private static Task UpsertRoleMetaAsync(IUmbracoDatabase db, string roleAlias, bool isTenantRole, string description) =>
        db.ExecuteAsync("""
            MERGE dcms_roles_meta AS target
            USING (VALUES (@0, @1, @2)) AS src (role_alias, is_tenant_role, description)
                ON target.role_alias = src.role_alias
            WHEN MATCHED THEN
                UPDATE SET is_tenant_role = src.is_tenant_role, description = src.description
            WHEN NOT MATCHED THEN
                INSERT (role_alias, is_tenant_role, description)
                VALUES (src.role_alias, src.is_tenant_role, src.description);
            """, roleAlias, isTenantRole ? 1 : 0, description);

    private static object ErrorEnvelope(string message, Exception? _ = null) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "ERROR", message },
    };

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public sealed class CreateRoleRequest
    {
        public string Name { get; set; } = null!;
        public string Alias { get; set; } = null!;
        public string? Icon { get; set; }
        public bool? IsTenantRole { get; set; }
        public string? Description { get; set; }
    }

    public sealed class UpdateRoleRequest
    {
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public bool? IsTenantRole { get; set; }
        public string? Description { get; set; }
    }

    public sealed class UpsertPermissionsRequest
    {
        public List<ActionEntry> Actions { get; set; } = [];

        public sealed class ActionEntry
        {
            public string Action { get; set; } = null!;
            public bool Granted { get; set; }
        }
    }

    private sealed class PermissionRow
    {
        public string RoleAlias { get; set; } = null!;
        public string Module { get; set; } = null!;
        public string Action { get; set; } = null!;
        public bool Granted { get; set; }
    }

    private sealed class GroupMemberCountRow
    {
        public int UserGroupId { get; set; }
        public int MemberCount { get; set; }
    }

    private sealed class RoleMetaRow
    {
        public string RoleAlias { get; set; } = null!;
        public bool IsTenantRole { get; set; }
        public string Description { get; set; } = "";
    }
}
