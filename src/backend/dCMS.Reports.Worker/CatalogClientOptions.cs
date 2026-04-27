namespace dCMS.Reports.Worker;

public sealed class CatalogClientOptions
{
    public const string SectionName = "Catalog";
    public const string HttpClientName = "CatalogInternal";

    public string? BaseUrl { get; set; }
    public string? InternalApiKey { get; set; }
}
