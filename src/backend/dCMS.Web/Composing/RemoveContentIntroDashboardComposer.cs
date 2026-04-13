using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;
using Umbraco.Cms.Core.DependencyInjection;

namespace dCMS.Web.Composing;

/// <summary>Removes the default Content "Getting Started" / intro dashboard tab from the backoffice.</summary>
public sealed class RemoveContentIntroDashboardComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.Dashboards().Remove<ContentDashboard>();
}
