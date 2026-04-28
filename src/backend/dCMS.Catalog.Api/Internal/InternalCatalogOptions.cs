namespace dCMS.Catalog.Api.Internal;

public sealed class InternalCatalogOptions
{
    public const string SectionName = "InternalCatalog";

    /// <summary>When non-empty, <c>/internal/catalog/*</c> is enabled and requires header <c>X-Internal-Api-Key</c> matching this value (SHA-256 constant-time compare).</summary>
    public string ApiKey { get; set; } = "";
}
