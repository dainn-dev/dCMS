using dCMS.Web.Sections;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sections;

namespace dCMS.Web.Composing;

public sealed class DcmsOrdersSectionComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Sections().InsertAfter<ContentSection, DcmsOrdersSection>();
        builder.AddNotificationHandler<UmbracoApplicationStartingNotification, GrantDcmsOrdersSectionNotificationHandler>();
    }
}
