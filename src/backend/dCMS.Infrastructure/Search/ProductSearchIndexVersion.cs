namespace dCMS.Infrastructure.Search;

/// <summary>Physical product-search index version (suffix <c>-v{N}</c>). Bump when mappings require a new backing index + reindex.</summary>
public static class ProductSearchIndexVersion
{
    public const int Latest = 1;
}
