using dCMS.Web.Sections;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sections;

namespace dCMS.Web.Composing;

/// <summary>Registers the dCMS Catalog backoffice section after Content and grants default group access (DAI-281).</summary>
public sealed class DcmsCatalogSectionComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Sections().InsertAfter<ContentSection, DcmsCatalogSection>();
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, GrantDcmsCatalogSectionNotificationHandler>();
    }
}
