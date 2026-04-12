using Umbraco.Cms.Core.Sections;

namespace dCMS.Web.Sections;

/// <summary>US-13 / DAI-281: dedicated backoffice area for Store Manager catalog tools (wizard, future screens).</summary>
public sealed class DcmsCatalogSection : ISection
{
    public const string SectionAlias = "dCMSCatalog";

    public string Alias => SectionAlias;

    public string Name => "dCMS Catalog";
}
