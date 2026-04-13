using dCMS.Web.Sections;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;

namespace dCMS.Web.Dashboards;

/// <summary>
/// Landing dashboard for the Orders section — renders the Order Processing view
/// so clicking "Orders" in the header always opens this page first.
/// </summary>
[Weight(10)]
public sealed class DcmsOrderProcessingDashboard : IDashboard
{
    public string Alias => "dcmsOrderProcessing";

    public string[] Sections => [DcmsOrdersSection.SectionAlias];

    public string View => "/App_Plugins/DcmsOrders/backoffice/orders/order-processing.html";

    public IAccessRule[] AccessRules => [];
}
