namespace dCMS.Infrastructure.Search;

public static class ElasticsearchIndexNames
{
    /// <summary>
    /// Logical product search name: Elasticsearch **alias** (US-8). Index/delete/search target this name;
    /// it resolves to a versioned backing index (e.g. <c>dcms-tenant-products-v1</c>).
    /// </summary>
    public static string Products(string tenantId) => ProductsAlias(tenantId);

    /// <inheritdoc cref="Products"/>
    public static string ProductsAlias(string tenantId) => PhysicalSlug(tenantId) is { } slug
        ? $"dcms-{slug}-products"
        : "dcms-unknown-products";

    /// <summary>Concrete backing index for a mapping version (never query this directly from the app except bootstrap/reindex).</summary>
    public static string ProductsBackingIndex(string tenantId, int mappingVersion) =>
        $"{ProductsAlias(tenantId)}-v{mappingVersion}";

    private static string? PhysicalSlug(string tenantId)
    {
        var t = tenantId.Trim().ToLowerInvariant();
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            t = t.Replace(c, '-');
        return string.IsNullOrEmpty(t) ? null : t;
    }
}
