using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace dCMS.Web.Composing;

/// <summary>Grants dCMS custom backoffice sections to default Admin and Editor groups on startup,
/// and ensures the 10 default dCMS user groups exist.</summary>
public sealed class GrantDcmsCustomSectionsNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IUserGroupService _userGroupService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly ILogger<GrantDcmsCustomSectionsNotificationHandler> _logger;

    /// <summary>Display names and aliases for the 10 default dCMS user groups.</summary>
    private static readonly (string Name, string Alias)[] DefaultGroups =
    [
        ("IT Administrator",          "dcmsItAdministrator"),
        ("System Administrator",      "dcmsSysAdministrator"),
        ("Ecommerce Manager",         "dcmsEcommerceManager"),
        ("Tenant Product Manager",    "dcmsTenantProductManager"),
        ("Tenant Inventory Manager",  "dcmsTenantInventoryManager"),
        ("Operations",                "dcmsOperations"),
        ("Finance",                   "dcmsFinance"),
        ("Brand Manager",             "dcmsBrandManager"),
        ("Product Upload",            "dcmsProductUpload"),
        ("Guest",                     "dcmsGuest"),
    ];

    public GrantDcmsCustomSectionsNotificationHandler(
        IUserGroupService userGroupService,
        IShortStringHelper shortStringHelper,
        ILogger<GrantDcmsCustomSectionsNotificationHandler> logger)
    {
        _userGroupService = userGroupService;
        _shortStringHelper = shortStringHelper;
        _logger = logger;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
            return;

        var sections = new[]
        {
            DcmsSectionAliases.EStore,
            DcmsSectionAliases.Orders,
            DcmsSectionAliases.Approval,
            DcmsSectionAliases.Reports,
            DcmsSectionAliases.Access,
        };
        // Built-in sections to hide because dCMS provides equivalents under Access (Users / Roles / Tenants).
        // Keep Content + Media + Settings + Members + Translation intact.
        var sectionsToRemove = new[]
        {
            Constants.Applications.Users,
        };
        var groupKeys = new[]
        {
            Constants.Security.AdminGroupKey,
            Constants.Security.EditorGroupKey,
        };

        try
        {
            foreach (var groupKey in groupKeys)
            {
                IUserGroup? group = await _userGroupService.GetAsync(groupKey).ConfigureAwait(false);
                if (group is null)
                    continue;

                var changed = false;
                foreach (var section in sections)
                {
                    if (group.AllowedSections.InvariantContains(section))
                        continue;

                    group.AddAllowedSection(section);
                    changed = true;
                }

                foreach (var section in sectionsToRemove)
                {
                    if (!group.AllowedSections.InvariantContains(section))
                        continue;

                    group.RemoveAllowedSection(section);
                    changed = true;
                }

                if (changed)
                    await _userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);
            }

            await EnsureDefaultUserGroupsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogDebug(ex, "Skipped custom section grants: Umbraco tables not ready yet.");
        }
    }

    private async Task EnsureDefaultUserGroupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // IUserGroupService.GetAllAsync(int skip, int take) returns PagedModel<IUserGroup> directly.
            var page = await _userGroupService.GetAllAsync(0, 1000).ConfigureAwait(false);
            var existingNames = page.Items
                .Select(g => g.Name ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, alias) in DefaultGroups)
            {
                if (existingNames.Contains(name))
                    continue;

                var group = new UserGroup(_shortStringHelper)
                {
                    Name = name,
                    Alias = alias,
                };

                await _userGroupService.CreateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);
                _logger.LogInformation("Created default dCMS user group: {GroupName} ({Alias})", name, alias);
            }
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogDebug(ex, "Skipped default user group creation: Umbraco tables not ready yet.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure default dCMS user groups.");
        }
    }
}
