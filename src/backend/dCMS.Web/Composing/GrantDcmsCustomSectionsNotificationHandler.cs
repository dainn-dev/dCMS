using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace dCMS.Web.Composing;

/// <summary>Grants dCMS custom backoffice sections to default Admin and Editor groups on startup.</summary>
public sealed class GrantDcmsCustomSectionsNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IUserGroupService _userGroupService;
    private readonly ILogger<GrantDcmsCustomSectionsNotificationHandler> _logger;

    public GrantDcmsCustomSectionsNotificationHandler(
        IUserGroupService userGroupService,
        ILogger<GrantDcmsCustomSectionsNotificationHandler> logger)
    {
        _userGroupService = userGroupService;
        _logger = logger;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
            return;

        var sections = new[] { DcmsSectionAliases.Orders, DcmsSectionAliases.EStore };
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

                if (changed)
                    await _userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey).ConfigureAwait(false);
            }
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogDebug(ex, "Skipped custom section grants: Umbraco tables not ready yet.");
        }
    }
}
