using System.Text.Json;
using dCMS.Core.Search;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Search;

/// <summary>US-7: Redis cache for catalog product search (SHA-256 key, TTL 30s).</summary>
public sealed class RedisCachingProductSearchQuery(
    IProductSearchQuery inner,
    IConnectionMultiplexer redis,
    ILogger<RedisCachingProductSearchQuery> logger) : IProductSearchQuery
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var hash = ProductSearchCacheKey.ComputeHash(query);
        var key = ProductSearchCacheKey.RedisKey(query.StoreId, hash);
        var db = redis.GetDatabase();
        try
        {
            var cached = await db.StringGetAsync(key).ConfigureAwait(false);
            if (cached.HasValue)
            {
                var dto = JsonSerializer.Deserialize<SearchResultCacheDto>(cached!, Json);
                if (dto is not null)
                    return dto.ToResult();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis product search read failed for store {StoreId}.", query.StoreId);
        }

        var result = await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = SearchResultCacheDto.FromResult(result);
            var json = JsonSerializer.Serialize(payload, Json);
            await db.StringSetAsync(key, json, Ttl).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis product search write failed for store {StoreId}.", query.StoreId);
        }

        return result;
    }

    private sealed class SearchResultCacheDto
    {
        public List<ItemCacheDto>? Items { get; set; }
        public long TotalCount { get; set; }
        public string? NextCursor { get; set; }
        public FacetsCacheDto? Facets { get; set; }

        public static SearchResultCacheDto FromResult(ProductSearchResult r) =>
            new()
            {
                Items = r.Items.Select(static i => new ItemCacheDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    NameByLocale = i.NameByLocale is Dictionary<string, string> d
                        ? d
                        : new Dictionary<string, string>(i.NameByLocale, StringComparer.Ordinal),
                    MinBasePrice = i.MinBasePrice,
                    HasInStockVariant = i.HasInStockVariant,
                    Slug = i.Slug
                }).ToList(),
                TotalCount = r.TotalCount,
                NextCursor = r.NextCursor,
                Facets = r.Facets is null
                    ? null
                    : new FacetsCacheDto
                    {
                        CategoryAncestors = r.Facets.CategoryAncestors
                            .Select(static b => new FacetBucketCacheDto { Key = b.Key, DocCount = b.DocCount }).ToList(),
                        AttributeTerms = r.Facets.AttributeTerms.ToDictionary(static kv => kv.Key,
                            static kv => kv.Value.Select(b => new FacetBucketCacheDto
                                { Key = b.Key, DocCount = b.DocCount }).ToList(), StringComparer.Ordinal),
                        PriceMin = r.Facets.PriceMin,
                        PriceMax = r.Facets.PriceMax
                    }
            };

        public ProductSearchResult ToResult()
        {
            var items = (Items ?? []).Select(static i => new ProductSearchItem(i.Id, i.Name,
                i.NameByLocale ?? new Dictionary<string, string>(StringComparer.Ordinal), i.MinBasePrice,
                i.HasInStockVariant, i.Slug)).ToList();

            SearchFacets? facets = null;
            if (Facets is not null)
            {
                var cats = (Facets.CategoryAncestors ?? [])
                    .Select(static b => new FacetTermBucket(b.Key, b.DocCount)).ToList();
                var attrs = Facets.AttributeTerms ?? new Dictionary<string, List<FacetBucketCacheDto>>(StringComparer.Ordinal);
                var attrRead = attrs.ToDictionary(static kv => kv.Key,
                    static kv => (IReadOnlyList<FacetTermBucket>)kv.Value
                        .Select(b => new FacetTermBucket(b.Key, b.DocCount)).ToList(), StringComparer.Ordinal);
                facets = new SearchFacets(cats, attrRead, Facets.PriceMin, Facets.PriceMax);
            }

            return new ProductSearchResult(items, TotalCount, NextCursor, facets);
        }
    }

    private sealed class ItemCacheDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public Dictionary<string, string>? NameByLocale { get; set; }
        public MoneyAmount MinBasePrice { get; set; } = new(0, "");
        public bool HasInStockVariant { get; set; }
        public string Slug { get; set; } = "";
    }

    private sealed class FacetsCacheDto
    {
        public List<FacetBucketCacheDto>? CategoryAncestors { get; set; }
        public Dictionary<string, List<FacetBucketCacheDto>>? AttributeTerms { get; set; }
        public long? PriceMin { get; set; }
        public long? PriceMax { get; set; }
    }

    private sealed class FacetBucketCacheDto
    {
        public string Key { get; set; } = "";
        public long DocCount { get; set; }
    }
}
