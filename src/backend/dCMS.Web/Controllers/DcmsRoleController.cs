using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Controllers;

/// <summary>Backoffice API for managing Umbraco UserGroups and their module permissions (dCMS Access §8).</summary>
[ApiController]
[Route("umbraco/dcms/api/roles")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class DcmsRoleController : ControllerBase
{
    private readonly IUserGroupService _userGroupService;
    private readonly IUserService _userService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IUmbracoDatabaseFactory _dbFactory;
    private readonly IEntityService _entityService;

    public DcmsRoleController(
        IUserGroupService userGroupService,
        IUserService userService,
        IShortStringHelper shortStringHelper,
        IUmbracoDatabaseFactory dbFactory,
        IEntityService entityService)
    {
        _userGroupService = userGroupService;
        _userService = userService;
        _shortStringHelper = shortStringHelper;
        _dbFactory = dbFactory;
        _entityService = entityService;
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

    // ── GET /{alias}/umbraco-permissions ──────────────────────────────────────
    /// <summary>
    /// DAI-671: Read built-in Umbraco section + start-node assignments for a role,
    /// so the dCMS Roles UI can manage Content/Media access (now that the built-in
    /// Users section is hidden).
    /// </summary>
    [HttpGet("{alias}/umbraco-permissions")]
    public async Task<IActionResult> GetUmbracoPermissions(string alias)
    {
        try
        {
            var group = await _userGroupService.GetAsync(alias).ConfigureAwait(false);
            if (group is null)
                return NotFound(ErrorEnvelope("Role not found."));

            return Ok(new
            {
                data = MapUmbracoPermissions(group),
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to read Umbraco permissions.", ex));
        }
    }

    // ── PUT /{alias}/umbraco-permissions ──────────────────────────────────────
    [HttpPut("{alias}/umbraco-permissions")]
    public async Task<IActionResult> UpdateUmbracoPermissions(
        string alias,
        [FromBody] UpdateUmbracoPermissionsRequest request)
    {
        try
        {
            var group = await _userGroupService.GetAsync(alias).ConfigureAwait(false);
            if (group is null)
                return NotFound(ErrorEnvelope("Role not found."));

            // Protected built-in groups: refuse changes (DAI-671 spec).
            if (IsProtectedGroup(group))
                return Forbid();

            // ── AllowedSections diff: only manage Content + Media (whitelist) ──
            var requested = new HashSet<string>(
                (request.Sections ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var manageable = new[] { Constants.Applications.Content, Constants.Applications.Media };
            foreach (var section in manageable)
            {
                var has = group.AllowedSections.InvariantContains(section);
                var want = requested.Contains(section);
                if (want && !has)
                    group.AddAllowedSection(section);
                else if (!want && has)
                    group.RemoveAllowedSection(section);
            }

            // ── Start nodes (single id each, scalar in IUserGroup) ──
            group.StartContentId = ResolveEntityIdFromKey(request.ContentStartNodeKey, UmbracoObjectTypes.Document);
            group.StartMediaId = ResolveEntityIdFromKey(request.MediaStartNodeKey, UmbracoObjectTypes.Media);

            await _userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);

            return Ok(new
            {
                data = MapUmbracoPermissions(group),
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update Umbraco permissions.", ex));
        }
    }

    // ── GET /{alias}/granular-permissions ─────────────────────────────────────
    /// <summary>
    /// DAI-671 follow-up: Per-content-node granular permissions for a role.
    /// Media nodes aren't supported by stock Umbraco v16 (no <c>MediaGranularPermission</c>).
    /// </summary>
    [HttpGet("{alias}/granular-permissions")]
    public async Task<IActionResult> GetGranularPermissions(string alias)
    {
        try
        {
            var group = await _userGroupService.GetAsync(alias).ConfigureAwait(false);
            if (group is null)
                return NotFound(ErrorEnvelope("Role not found."));

            // Group by node key → list of permission verbs.
            var grouped = group.GranularPermissions
                .OfType<DocumentGranularPermission>()
                .Where(d => d.Key != Guid.Empty && !string.IsNullOrEmpty(d.Permission))
                .GroupBy(d => d.Key)
                .Select(g => new
                {
                    nodeKey = g.Key,
                    permissions = g.Select(p => p.Permission).Distinct().OrderBy(p => p).ToArray(),
                })
                .ToArray();

            return Ok(new
            {
                data = new
                {
                    contentNodes = grouped,
                    availablePermissions = DcmsContentActions.All,
                    isProtected = IsProtectedGroup(group),
                },
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to read granular permissions.", ex));
        }
    }

    // ── PUT /{alias}/granular-permissions ─────────────────────────────────────
    [HttpPut("{alias}/granular-permissions")]
    public async Task<IActionResult> UpdateGranularPermissions(
        string alias,
        [FromBody] UpdateGranularPermissionsRequest request)
    {
        try
        {
            var group = await _userGroupService.GetAsync(alias).ConfigureAwait(false);
            if (group is null)
                return NotFound(ErrorEnvelope("Role not found."));

            if (IsProtectedGroup(group))
                return Forbid();

            // Drop existing document-node entries; replace wholesale with incoming set.
            // (Property-value entries, if any, are left untouched.)
            foreach (var existing in group.GranularPermissions
                         .OfType<DocumentGranularPermission>()
                         .ToList())
            {
                group.GranularPermissions.Remove(existing);
            }

            var validVerbs = DcmsContentActions.All.Select(a => a.alias)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var node in request.ContentNodes ?? Array.Empty<ContentNodeGrant>())
            {
                if (node.NodeKey is null || node.NodeKey == Guid.Empty) continue;
                if (node.Permissions is null) continue;

                foreach (var verb in node.Permissions.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(verb) || !validVerbs.Contains(verb)) continue;
                    group.GranularPermissions.Add(new DocumentGranularPermission
                    {
                        Key = node.NodeKey.Value,
                        Permission = verb,
                    });
                }
            }

            await _userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);

            // Re-serialize current state to mirror the GET contract.
            var grouped = group.GranularPermissions
                .OfType<DocumentGranularPermission>()
                .Where(d => d.Key != Guid.Empty && !string.IsNullOrEmpty(d.Permission))
                .GroupBy(d => d.Key)
                .Select(g => new
                {
                    nodeKey = g.Key,
                    permissions = g.Select(p => p.Permission).Distinct().OrderBy(p => p).ToArray(),
                })
                .ToArray();

            return Ok(new
            {
                data = new
                {
                    contentNodes = grouped,
                    availablePermissions = DcmsContentActions.All,
                    isProtected = IsProtectedGroup(group),
                },
                meta = (object?)null,
                error = (object?)null,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update granular permissions.", ex));
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

    /// <summary>
    /// DAI-671: Built-in groups whose Umbraco permissions cannot be touched from the dCMS UI
    /// (Admins always have everything; Sensitive data is a security boundary).
    /// </summary>
    private static bool IsProtectedGroup(IUserGroup g) =>
        string.Equals(g.Alias, Constants.Security.AdminGroupAlias, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(g.Alias, "sensitiveData", StringComparison.OrdinalIgnoreCase);

    private object MapUmbracoPermissions(IUserGroup g)
    {
        var manageable = new[] { Constants.Applications.Content, Constants.Applications.Media };
        var sections = g.AllowedSections
            .Where(s => manageable.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return new
        {
            sections,
            contentStartNodeKey = ResolveEntityKeyFromId(g.StartContentId, UmbracoObjectTypes.Document),
            mediaStartNodeKey = ResolveEntityKeyFromId(g.StartMediaId, UmbracoObjectTypes.Media),
            isProtected = IsProtectedGroup(g),
        };
    }

    private int? ResolveEntityIdFromKey(Guid? key, UmbracoObjectTypes objectType)
    {
        if (key is null || key == Guid.Empty) return null;
        var attempt = _entityService.GetId(key.Value, objectType);
        return attempt.Success ? attempt.Result : null;
    }

    private Guid? ResolveEntityKeyFromId(int? id, UmbracoObjectTypes objectType)
    {
        if (id is null or <= 0) return null;
        var attempt = _entityService.GetKey(id.Value, objectType);
        return attempt.Success ? attempt.Result : null;
    }

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

    /// <summary>DAI-671: Payload for PUT /roles/{alias}/umbraco-permissions.</summary>
    public sealed class UpdateUmbracoPermissionsRequest
    {
        /// <summary>Subset of Umbraco built-in section aliases to allow. Whitelist enforced server-side: only "content" + "media".</summary>
        public string[]? Sections { get; set; }

        /// <summary>Start-node key for Content tree (null = no restriction / root).</summary>
        public Guid? ContentStartNodeKey { get; set; }

        /// <summary>Start-node key for Media tree (null = no restriction / root).</summary>
        public Guid? MediaStartNodeKey { get; set; }
    }

    /// <summary>DAI-671 follow-up: payload for PUT /roles/{alias}/granular-permissions.</summary>
    public sealed class UpdateGranularPermissionsRequest
    {
        public ContentNodeGrant[]? ContentNodes { get; set; }
    }

    public sealed class ContentNodeGrant
    {
        public Guid? NodeKey { get; set; }
        public string[]? Permissions { get; set; }
    }

    /// <summary>
    /// Catalog of Umbraco core document actions surfaced in the dCMS Roles UI.
    /// Verb strings are <c>IAction.Letter</c> values (the <c>Permission</c> field of <see cref="DocumentGranularPermission"/>).
    /// Source: <c>Umbraco.Cms.Core.Actions.Action*</c> reflection (v16.5.1).
    /// </summary>
    private static class DcmsContentActions
    {
        public static readonly (string alias, string label, string category)[] All =
        [
            // category aliases match Constants.Conventions.PermissionCategories
            ("Umb.Document.Read",                "Browse",            "content"),
            ("Umb.Document.Update",              "Update",            "content"),
            ("Umb.Document.Create",              "Create",            "content"),
            ("Umb.Document.Delete",              "Delete",            "content"),
            ("Umb.Document.Publish",             "Publish",           "content"),
            ("Umb.Document.Unpublish",           "Unpublish",         "content"),
            ("Umb.Document.Sort",                "Sort",              "structure"),
            ("Umb.Document.Move",                "Move",              "structure"),
            ("Umb.Document.Duplicate",           "Copy",              "structure"),
            ("Umb.DocumentRecycleBin.Restore",   "Restore",           "structure"),
            ("Umb.Document.Rollback",            "Rollback",          "administration"),
            ("Umb.Document.Notifications",       "Notifications",     "administration"),
            ("Umb.Document.Permissions",         "Permissions",       "administration"),
            ("Umb.Document.PublicAccess",        "Public Access",     "administration"),
            ("Umb.Document.CultureAndHostnames", "Culture & Hostnames","administration"),
            ("Umb.Document.CreateBlueprint",     "Create Blueprint",  "other"),
        ];
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
