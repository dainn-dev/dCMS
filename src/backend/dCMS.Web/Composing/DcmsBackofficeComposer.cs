using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace dCMS.Web.Composing;

/// <summary>Registers dCMS backoffice startup handlers (section grants for default groups).</summary>
public sealed class DcmsBackofficeComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, GrantDcmsCustomSectionsNotificationHandler>();
}
