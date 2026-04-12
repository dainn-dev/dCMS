using dCMS.Web.CatalogProxy;
using dCMS.Web.Sections;
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
/// </summary>
public sealed class GrantDcmsCatalogSectionNotificationHandler : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private readonly IUserService _userService;

    public GrantDcmsCatalogSectionNotificationHandler(IUserService userService) =>
        _userService = userService;

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        var section = DcmsCatalogSection.SectionAlias;
        var aliases = new[]
        {
            Constants.Security.AdminGroupAlias,
            Constants.Security.EditorGroupAlias,
            CatalogBackofficeRoleMapping.BrandManagerGroupAlias,
            CatalogBackofficeRoleMapping.StoreStaffGroupAlias,
        };

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
}
