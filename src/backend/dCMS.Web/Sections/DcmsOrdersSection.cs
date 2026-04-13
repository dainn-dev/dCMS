using Umbraco.Cms.Core.Sections;

namespace dCMS.Web.Sections;

/// <summary>Top-level backoffice section for commerce orders (header ribbon).</summary>
public sealed class DcmsOrdersSection : ISection
{
    public const string SectionAlias = "dcmsOrders";

    public string Alias => SectionAlias;

    public string Name => "Orders";
}
