using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;

namespace dCMS.Web.Dashboards;

/// <summary>Content section dashboard tab — sales KPIs + chart shell (wire to Order/analytics APIs later).</summary>
[Weight(12)]
public sealed class DcmsSalesOverviewDashboard : IDashboard
{
    public const string DashboardAlias = "dcmsDashboard";

    public string Alias => DashboardAlias;

    public string[] Sections => new[] { Constants.Applications.Content };

    public string View => "/App_Plugins/DcmsBackoffice/dashboards/sales-overview.html";

    public IAccessRule[] AccessRules => Array.Empty<IAccessRule>();
}
