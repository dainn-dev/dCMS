using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using dCMS.Infrastructure.Middleware;
using dCMS.Web.Access.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Access.Services;

/// <summary>
/// Resolves module/action permissions for an Umbraco backoffice user by:
/// 1. Looking up the user's UserGroup aliases via <see cref="IUserService.GetUserById"/>.
/// 2. Querying <c>dcms_role_module_permissions</c> in the Umbraco SQL Server DB using NPoco.
/// Cached behind <see cref="IPermissionCache"/> (memory L1 + Redis L2) — see DAI-683.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IUserService _userService;
    private readonly IUmbracoDatabaseFactory _dbFactory;
    private readonly IPermissionCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IUserService userService,
        IUmbracoDatabaseFactory dbFactory,
        IPermissionCache cache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PermissionService> logger)
    {
        _userService = userService;
        _dbFactory = dbFactory;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(int userId, string module, string action, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var aliases = GetGroupAliases(userId);
            if (aliases.Count == 0)
                return false;

            var tenantId = ResolveTenantId();
            var rolesHash = ComputeRolesHash(aliases);
            var cached = await _cache.TryGetGrantedActionsAsync(tenantId, userId, rolesHash, module, ct).ConfigureAwait(false);
            if (cached is not null)
                return cached.Contains(action, StringComparer.OrdinalIgnoreCase);

            try
            {
                using var db = _dbFactory.CreateDatabase();

                var actions = await QueryGrantedActionsAsync(db, aliases, module).ConfigureAwait(false);
                await _cache.SetGrantedActionsAsync(tenantId, userId, rolesHash, module, actions, ct).ConfigureAwait(false);
                return actions.Contains(action, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check permission for userId={UserId} module={Module} action={Action}", userId, module, action);
                return false;
            }
        }
        finally
        {
            PermissionCacheMetrics.CheckDuration.Observe(sw.Elapsed.TotalSeconds);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetGrantedActionsAsync(int userId, string module, CancellationToken ct = default)
    {
        var aliases = GetGroupAliases(userId);
        if (aliases.Count == 0)
            return Array.Empty<string>();

        var tenantId = ResolveTenantId();
        var rolesHash = ComputeRolesHash(aliases);
        var cached = await _cache.TryGetGrantedActionsAsync(tenantId, userId, rolesHash, module, ct).ConfigureAwait(false);
        if (cached is not null)
            return cached;

        try
        {
            using var db = _dbFactory.CreateDatabase();

            var rows = await QueryGrantedActionsAsync(db, aliases, module).ConfigureAwait(false);
            await _cache.SetGrantedActionsAsync(tenantId, userId, rolesHash, module, rows, ct).ConfigureAwait(false);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get granted actions for userId={UserId} module={Module}", userId, module);
            return Array.Empty<string>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    private static async Task<IReadOnlyList<string>> QueryGrantedActionsAsync(
        IUmbracoDatabase db,
        IReadOnlyList<string> aliases,
        string module)
    {
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

    private string ResolveTenantId()
    {
        try
        {
            var http = _httpContextAccessor.HttpContext;
            var tid = http?.Items.TryGetValue(HostTenantRoutingMiddleware.ResolvedTenantIdItemKey, out var v) == true
                ? v?.ToString()
                : null;
            return string.IsNullOrWhiteSpace(tid) ? "default" : tid!;
        }
        catch
        {
            return "default";
        }
    }

    private static string ComputeRolesHash(IReadOnlyList<string> aliases)
    {
        var joined = string.Join('|', aliases.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

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
