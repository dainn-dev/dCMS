namespace dCMS.Approval.Api.Routes.Subjects;

/// <summary>Phase C: HTTP client options for cross-service approval subjects.</summary>
public sealed class PromotionsApiClientOptions
{
    public const string HttpClientName = "PromotionsInternal";
    public const string HeaderName = "X-Internal-Api-Key";

    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class CatalogApiClientOptions
{
    public const string HttpClientName = "CatalogInternal";
    public const string HeaderName = "X-Internal-Api-Key";

    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}
