namespace dCMS.Infrastructure.Search;

public sealed class CatalogSearchIndexingOptions
{
    public const string SectionName = "CatalogSearchIndexing";

    /// <summary>Until Stores table exposes currency per channel (spec), default for ES <c>storeCurrency</c> / money fields.</summary>
    public string DefaultStoreCurrency { get; set; } = "VND";
}
