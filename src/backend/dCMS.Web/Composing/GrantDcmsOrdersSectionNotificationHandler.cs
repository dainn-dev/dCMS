using dCMS.Web.Sections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace dCMS.Web.Composing;

/// <summary>Grants the Orders section to default backoffice groups on startup.</summary>
public sealed class GrantDcmsOrdersSectionNotificationHandler : INotificationHandler<UmbracoApplicationStartingNotification>
{
    private readonly IUserService _userService;
    private readonly ILogger<GrantDcmsOrdersSectionNotificationHandler> _logger;

    public GrantDcmsOrdersSectionNotificationHandler(
        IUserService userService,
        ILogger<GrantDcmsOrdersSectionNotificationHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
            return;

        var section = DcmsOrdersSection.SectionAlias;
        var aliases = new[]
        {
            Constants.Security.AdminGroupAlias,
            Constants.Security.EditorGroupAlias,
        };

        try
        {
            foreach (var alias in aliases)
            {
                var group = _userService.GetUserGroupByAlias(alias);
                if (group is not UserGroup editable)
                    continue;

                if (editable.AllowedSections.InvariantContains(section))
                    continue;

                editable.AddAllowedSection(section);
                _userService.Save(editable, new[] { Constants.Security.SuperUserId });
            }
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogDebug(ex, "Skipped Orders section grants: Umbraco tables not ready yet.");
        }
    }
}
