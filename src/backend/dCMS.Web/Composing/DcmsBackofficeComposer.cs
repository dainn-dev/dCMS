using dCMS.Web.ContentApproval;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace dCMS.Web.Composing;

/// <summary>Registers dCMS backoffice startup handlers (section grants for default groups) and DAI-721 content-approval interceptors.</summary>
public sealed class DcmsBackofficeComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, GrantDcmsCustomSectionsNotificationHandler>();

        // DAI-721: content publish gate + approval client.
        builder.Services.AddHttpClient(ApprovalApiClient.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddSingleton<ApprovalApiClient>();
        builder.AddNotificationAsyncHandler<ContentPublishingNotification, ContentPublishingApprovalHandler>();
    }
}
