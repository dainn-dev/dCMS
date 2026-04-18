using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Controllers;

/// <summary>Backoffice API for managing Umbraco users (dCMS Access module §8).</summary>
[ApiController]
[Route("umbraco/dcms/api/users")]
[Authorize(Policy = "BackOfficeAccess")]
public sealed class DcmsUserController : ControllerBase
{
    private static readonly Regex PasswordRegex = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        RegexOptions.Compiled);

    private readonly IUserService _userService;
    private readonly IUserGroupService _userGroupService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUmbracoDatabaseFactory _dbFactory;

    public DcmsUserController(
        IUserService userService,
        IUserGroupService userGroupService,
        IPasswordHasher passwordHasher,
        IUmbracoDatabaseFactory dbFactory)
    {
        _userService = userService;
        _userGroupService = userGroupService;
        _passwordHasher = passwordHasher;
        _dbFactory = dbFactory;
    }

    // ── GET / ────────────────────────────────────────────────────────────────
    /// <summary>GET umbraco/dcms/api/users?page=1&amp;pageSize=20&amp;search=</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 20;

        try
        {
            // IUserService.GetAllAsync returns Attempt<PagedModel<IUser>, UserOperationStatus>
            var skip = (page - 1) * pageSize;
            var attempt = await _userService.GetAllAsync(Constants.Security.SuperUserKey, skip, pageSize)
                .ConfigureAwait(false);

            IEnumerable<IUser> items = attempt.Result?.Items ?? Enumerable.Empty<IUser>();
            long total = attempt.Result?.Total ?? 0;

            if (!string.IsNullOrWhiteSpace(search))
            {
                // For search, get all and filter client-side (no server-side text search in GetAllAsync)
                var allAttempt = await _userService.GetAllAsync(Constants.Security.SuperUserKey, 0, int.MaxValue)
                    .ConfigureAwait(false);
                var filtered = (allAttempt.Result?.Items ?? Enumerable.Empty<IUser>())
                    .Where(u =>
                        (u.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        u.Username.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                total = filtered.Count;
                items = filtered.Skip(skip).Take(pageSize);
            }

            return Ok(new
            {
                data = items.Select(MapUser),
                meta = new { page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) },
                error = (object?)null,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to retrieve users.", ex));
        }
    }

    // ── POST / ───────────────────────────────────────────────────────────────
    /// <summary>POST umbraco/dcms/api/users — create a new backoffice user.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ValidationEnvelope(ModelState));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorEnvelope("Username is required."));

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(ErrorEnvelope("A valid email address is required."));

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ErrorEnvelope("Name is required."));

        if (!PasswordRegex.IsMatch(request.Password ?? string.Empty))
            return BadRequest(ErrorEnvelope("Password must be at least 8 characters and contain uppercase, lowercase, and a digit."));

        try
        {
            var existing = _userService.GetByEmail(request.Email);
            if (existing is not null)
                return Conflict(ErrorEnvelope("A user with this email already exists."));

            var createModel = new UserCreateModel
            {
                UserName = request.Username,
                Email = request.Email,
                Name = request.Name,
            };

            var attempt = await _userService.CreateAsync(Constants.Security.SuperUserKey, createModel, request.Active)
                .ConfigureAwait(false);

            if (!attempt.Success || attempt.Result?.CreatedUser is null)
                return BadRequest(ErrorEnvelope($"Failed to create user: {attempt.Status}"));

            var user = attempt.Result.CreatedUser;

            // Set password via hash (admin-set, no old password required)
            user.RawPasswordValue = _passwordHasher.HashPassword(request.Password!);
            _userService.Save(user);

            // Assign user group if provided
            if (!string.IsNullOrWhiteSpace(request.UserGroupAlias))
            {
                var group = await _userGroupService.GetAsync(request.UserGroupAlias).ConfigureAwait(false);
                if (group is not null)
                    await _userGroupService.UpdateUserGroupsOnUsersAsync(
                        new HashSet<Guid> { group.Key },
                        new HashSet<Guid> { user.Key },
                        Constants.Security.SuperUserKey).ConfigureAwait(false);
            }

            return Ok(new { data = MapUser(user), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to create user.", ex));
        }
    }

    // ── GET /{id} ─────────────────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        try
        {
            var user = _userService.GetUserById(id);
            if (user is null)
                return NotFound(ErrorEnvelope("User not found."));

            return Ok(new { data = MapUser(user), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to retrieve user.", ex));
        }
    }

    // ── PUT /{id} ─────────────────────────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ValidationEnvelope(ModelState));

        try
        {
            var user = _userService.GetUserById(id);
            if (user is null)
                return NotFound(ErrorEnvelope("User not found."));

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (!request.Email.Contains('@'))
                    return BadRequest(ErrorEnvelope("A valid email address is required."));
                user.Email = request.Email;
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
                user.Name = request.Name;

            if (request.Active.HasValue)
                user.IsApproved = request.Active.Value;

            _userService.Save(user);

            if (!string.IsNullOrWhiteSpace(request.UserGroupAlias))
            {
                var group = await _userGroupService.GetAsync(request.UserGroupAlias).ConfigureAwait(false);
                if (group is not null)
                    await _userGroupService.UpdateUserGroupsOnUsersAsync(
                        new HashSet<Guid> { group.Key },
                        new HashSet<Guid> { user.Key },
                        Constants.Security.SuperUserKey).ConfigureAwait(false);
            }

            return Ok(new { data = MapUser(user), meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update user.", ex));
        }
    }

    // ── DELETE /{id} ──────────────────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try
        {
            var user = _userService.GetUserById(id);
            if (user is null)
                return NotFound(ErrorEnvelope("User not found."));

            _userService.Delete(user, true);
            return Ok(new { data = new { deleted = true }, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to delete user.", ex));
        }
    }

    // ── POST /{id}/change-password ────────────────────────────────────────────
    [HttpPost("{id:int}/change-password")]
    public IActionResult ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        if (!PasswordRegex.IsMatch(request.NewPassword ?? string.Empty))
            return BadRequest(ErrorEnvelope("Password must be at least 8 characters and contain uppercase, lowercase, and a digit."));

        try
        {
            var user = _userService.GetUserById(id);
            if (user is null)
                return NotFound(ErrorEnvelope("User not found."));

            user.RawPasswordValue = _passwordHasher.HashPassword(request.NewPassword!);
            _userService.Save(user);

            return Ok(new { data = new { passwordChanged = true }, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to change password.", ex));
        }
    }

    // ── GET /export ───────────────────────────────────────────────────────────
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format = "json")
    {
        try
        {
            var allAttempt = await _userService.GetAllAsync(Constants.Security.SuperUserKey, 0, int.MaxValue)
                .ConfigureAwait(false);

            var items = (allAttempt.Result?.Items ?? Enumerable.Empty<IUser>()).Select(MapUser).ToList();

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                sb.AppendLine("id,username,email,name,isActive,groups");
                foreach (var u in items)
                    sb.AppendLine($"{u.id},{EscapeCsv(u.username)},{EscapeCsv(u.email)},{EscapeCsv(u.name)},{u.isActive},{EscapeCsv(string.Join("|", u.groups))}");

                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "users.csv");
            }

            return Ok(new { data = items, meta = new { total = items.Count }, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to export users.", ex));
        }
    }

    // ── GET /{id}/notifications ───────────────────────────────────────────────
    [HttpGet("{id:int}/notifications")]
    public async Task<IActionResult> GetNotifications(int id)
    {
        try
        {
            var user = _userService.GetUserById(id);
            if (user is null)
                return NotFound(ErrorEnvelope("User not found."));

            var userId = user.Key.ToString();
            using var db = _dbFactory.CreateDatabase();

            var rows = await db.FetchAsync<NotificationPrefRow>(
                "SELECT id, user_id, notification_type, enabled, tenant_ids FROM dcms_user_notification_prefs WHERE user_id = @0",
                userId).ConfigureAwait(false);

            return Ok(new { data = rows, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to retrieve notification preferences.", ex));
        }
    }

    // ── PUT /{id}/notifications ───────────────────────────────────────────────
    [HttpPut("{id:int}/notifications")]
    public async Task<IActionResult> UpdateNotifications(int id, [FromBody] List<UpsertNotificationPrefRequest> prefs)
    {
        if (prefs is null || prefs.Count == 0)
            return BadRequest(ErrorEnvelope("Notification preferences list is required."));

        try
        {
            var user = _userService.GetUserById(id);
            if (user is null)
                return NotFound(ErrorEnvelope("User not found."));

            var userId = user.Key.ToString();
            using var db = _dbFactory.CreateDatabase();

            foreach (var pref in prefs)
            {
                if (string.IsNullOrWhiteSpace(pref.NotificationType))
                    continue;

                // SQL Server MERGE (T-SQL upsert)
                await db.ExecuteAsync("""
                    MERGE dcms_user_notification_prefs AS target
                    USING (VALUES (@0, @1, @2, @3)) AS src (user_id, notification_type, enabled, tenant_ids)
                        ON target.user_id = src.user_id AND target.notification_type = src.notification_type
                    WHEN MATCHED THEN
                        UPDATE SET enabled = src.enabled, tenant_ids = src.tenant_ids
                    WHEN NOT MATCHED THEN
                        INSERT (user_id, notification_type, enabled, tenant_ids)
                        VALUES (src.user_id, src.notification_type, src.enabled, src.tenant_ids);
                    """,
                    userId, pref.NotificationType, pref.Enabled, pref.TenantIds).ConfigureAwait(false);
            }

            return Ok(new { data = new { updated = prefs.Count }, meta = (object?)null, error = (object?)null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ErrorEnvelope("Failed to update notification preferences.", ex));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserSummary MapUser(IUser u) => new(
        u.Id,
        u.Key,
        u.Username,
        u.Email ?? string.Empty,
        u.Name ?? string.Empty,
        u.IsApproved,
        u.Groups.Select(g => g.Alias).ToArray(),
        u.CreateDate,
        u.UpdateDate);

    private static string EscapeCsv(string? value)
    {
        if (value is null) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static object ErrorEnvelope(string message, Exception? _ = null) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "ERROR", message },
    };

    private static object ValidationEnvelope(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new
        {
            code = "VALIDATION_ERROR",
            message = "Input validation failed.",
            fields = modelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray()),
        },
    };

    // ── Request / response types ──────────────────────────────────────────────

    /// <summary>Anonymous record substitute — strongly typed for CSV serialisation.</summary>
    private sealed record UserSummary(
        int id,
        Guid key,
        string username,
        string email,
        string name,
        bool isActive,
        string[] groups,
        DateTime createdAt,
        DateTime updatedAt);

    public sealed class CreateUserRequest
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? UserGroupAlias { get; set; }
        public bool Active { get; set; } = true;
    }

    public sealed class UpdateUserRequest
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? UserGroupAlias { get; set; }
        public bool? Active { get; set; }
    }

    public sealed class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = null!;
    }

    public sealed class UpsertNotificationPrefRequest
    {
        public string NotificationType { get; set; } = null!;
        public bool Enabled { get; set; }
        public string? TenantIds { get; set; }
    }

    private sealed class NotificationPrefRow
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public string NotificationType { get; set; } = null!;
        public bool Enabled { get; set; }
        public string? TenantIds { get; set; }
    }
}
