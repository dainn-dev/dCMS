using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dCMS.Core.Search;

/// <summary>Stable Redis cache key for product search (catalog spec §4 — sorted params + SHA-256).</summary>
public static class ProductSearchCacheKey
{
    public static string ComputeHash(ProductSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["store"] = query.StoreId,
            ["tenant"] = query.TenantId,
            ["q"] = query.Keyword?.ToLowerInvariant().Trim() ?? "",
            ["instock"] = query.InStockOnly?.ToString() ?? "",
            ["cat"] = query.CategoryAncestorId?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["min"] = query.MinPriceAmount?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["max"] = query.MaxPriceAmount?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["sort"] = query.Sort.ToString(),
            ["size"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
            ["cursor"] = query.SearchAfterCursor ?? "",
            ["facets"] = query.IncludeFacets.ToString(),
        };
        if (query.AttributeFilters is { Count: > 0 })
        {
            foreach (var kv in query.AttributeFilters.OrderBy(static x => x.Key, StringComparer.Ordinal))
                parts[$"attr.{kv.Key}"] = kv.Value ?? string.Empty;
        }

        var canonical = string.Join("&", parts.Select(static p => $"{p.Key}={p.Value}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string RedisKey(string storeId, string hash) => $"dcms:search:{storeId}:{hash}";
}
