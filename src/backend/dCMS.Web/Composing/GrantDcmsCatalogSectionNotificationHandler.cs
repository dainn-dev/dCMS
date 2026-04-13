using dCMS.Web.CatalogProxy;
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

/// <summary>
/// Grants the dCMS Catalog section to default and optional dCMS groups on startup (DAI-281 / DAI-280).
/// Idempotent: skips if the section is already allowed.
/// Runs on <see cref="UmbracoApplicationStartingNotification"/> at <see cref="RuntimeLevel.Run"/> so SQL Server
/// fresh installs are not queried before Umbraco schema exists (avoids invalid object name on <c>umbracoUserGroup</c>).
/// </summary>
public sealed class GrantDcmsCatalogSectionNotificationHandler : INotificationHandler<UmbracoApplicationStartingNotification>
{
    private readonly IUserService _userService;
    private readonly ILogger<GrantDcmsCatalogSectionNotificationHandler> _logger;

    public GrantDcmsCatalogSectionNotificationHandler(
        IUserService userService,
        ILogger<GrantDcmsCatalogSectionNotificationHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
            return;

        var section = DcmsCatalogSection.SectionAlias;
        var aliases = new[]
        {
            Constants.Security.AdminGroupAlias,
            Constants.Security.EditorGroupAlias,
            CatalogBackofficeRoleMapping.BrandManagerGroupAlias,
            CatalogBackofficeRoleMapping.StoreStaffGroupAlias,
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
            // Schema not created yet (e.g. first request still running install); grants run on next boot / recycle.
            _logger.LogDebug(ex, "Skipped dCMS Catalog section grants: Umbraco tables not ready yet.");
        }
    }
}
