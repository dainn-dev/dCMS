using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Access.Services;

/// <summary>
/// Resolves module/action permissions for an Umbraco backoffice user by:
/// 1. Looking up the user's UserGroup aliases via <see cref="IUserService.GetUserById"/>.
/// 2. Querying <c>dcms_role_module_permissions</c> in the Umbraco SQL Server DB using NPoco.
///
/// TODO: Add Redis/memory cache here to avoid repeated DB hits (target: 2000 CCU).
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IUserService _userService;
    private readonly IUmbracoDatabaseFactory _dbFactory;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IUserService userService,
        IUmbracoDatabaseFactory dbFactory,
        ILogger<PermissionService> logger)
    {
        _userService = userService;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(int userId, string module, string action, CancellationToken ct = default)
    {
        var aliases = GetGroupAliases(userId);
        if (aliases.Count == 0)
            return false;

        try
        {
            using var db = _dbFactory.CreateDatabase();

            // NPoco positional parameters: @0, @1, ...
            // Build a SQL IN clause with one parameter per alias.
            var inClause = string.Join(", ", aliases.Select((_, i) => $"@{i + 2}"));
            var sql = $"""
                SELECT COUNT(1)
                FROM dcms_role_module_permissions
                WHERE module = @0
                  AND action = @1
                  AND granted = 1
                  AND role_alias IN ({inClause})
                """;

            var args = new List<object> { module, action };
            args.AddRange(aliases.Cast<object>());

            var count = await db.ExecuteScalarAsync<int>(sql, args.ToArray()).ConfigureAwait(false);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check permission for userId={UserId} module={Module} action={Action}", userId, module, action);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetGrantedActionsAsync(int userId, string module, CancellationToken ct = default)
    {
        var aliases = GetGroupAliases(userId);
        if (aliases.Count == 0)
            return Array.Empty<string>();

        try
        {
            using var db = _dbFactory.CreateDatabase();

            var inClause = string.Join(", ", aliases.Select((_, i) => $"@{i + 1}"));
            var sql = $"""
                SELECT DISTINCT action
                FROM dcms_role_module_permissions
                WHERE module = @0
                  AND granted = 1
                  AND role_alias IN ({inClause})
                """;

            var args = new List<object> { module };
            args.AddRange(aliases.Cast<object>());

            var rows = await db.FetchAsync<string>(sql, args.ToArray()).ConfigureAwait(false);
            return rows.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get granted actions for userId={UserId} module={Module}", userId, module);
            return Array.Empty<string>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    private IReadOnlyList<string> GetGroupAliases(int userId)
    {
        try
        {
            var user = _userService.GetUserById(userId);
            if (user is null)
                return Array.Empty<string>();

            return user.Groups
                .Select(g => g.Alias)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve UserGroups for userId={UserId}", userId);
            return Array.Empty<string>();
        }
    }
}
