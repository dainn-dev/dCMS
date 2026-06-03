using System.Globalization;
using System.Text;
using System.Text.Json;
using dCMS.Core.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Search;

public sealed class ElasticsearchProductSearchService(
    ElasticsearchClientFactory elasticsearchFactory,
    ILogger<ElasticsearchProductSearchService> logger) : IProductSearchQuery
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var size = Math.Clamp(query.PageSize, 1, 50);
        var client = elasticsearchFactory.Client;
        var index = ElasticsearchIndexNames.Products(query.TenantId);
        var searchAfter = DecodeSearchAfter(query.SearchAfterCursor);

        var filters = new List<Query>
        {
            Query.Term(new TermQuery(Field.FromString("storeId")!) { Value = FieldValue.String(query.StoreId) }),
            Query.Term(new TermQuery(Field.FromString("tenantId")!) { Value = FieldValue.String(query.TenantId) })
        };

        // Status scope: null/empty → storefront default ("active" only). Admin can request additional statuses.
        if (query.Statuses is { Count: > 0 } statuses)
        {
            var statusShould = statuses
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Query.Term(new TermQuery(Field.FromString("status")!) { Value = FieldValue.String(s) }))
                .ToList();
            if (statusShould.Count > 0)
                filters.Add(Query.Bool(new BoolQuery { Should = statusShould, MinimumShouldMatch = 1 }));
        }
        else
        {
            filters.Add(Query.Term(new TermQuery(Field.FromString("status")!) { Value = FieldValue.String("active") }));
        }

        if (query.InStockOnly == true)
            filters.Add(Query.Term(new TermQuery(Field.FromString("hasInStockVariant")!) { Value = FieldValue.Boolean(true) }));

        if (query.CategoryAncestorId is int cid && cid > 0)
            filters.Add(Query.Term(new TermQuery(Field.FromString("categoryAncestors")!) { Value = FieldValue.Long(cid) }));

        if (!string.IsNullOrWhiteSpace(query.BrandId))
            filters.Add(Query.Term(new TermQuery(Field.FromString("brandId")!) { Value = FieldValue.String(query.BrandId.Trim()) }));

        if (query.MinPriceAmount is not null || query.MaxPriceAmount is not null)
        {
            filters.Add(Query.Range(new NumberRangeQuery(Field.FromString("minBasePrice.amount")!)
            {
                Gte = query.MinPriceAmount,
                Lte = query.MaxPriceAmount
            }));
        }

        if (query.AttributeFilters is { Count: > 0 })
        {
            foreach (var kv in query.AttributeFilters)
            {
                var productAttr = Query.Term(new TermQuery(Field.FromString($"attributes.{kv.Key}")!)
                    { Value = FieldValue.String(kv.Value) });

                var nestedMust = new List<Query>
                {
                    Query.Term(new TermQuery(Field.FromString($"variants.attributes.{kv.Key}")!)
                        { Value = FieldValue.String(kv.Value) }),
                    Query.Term(new TermQuery(Field.FromString("variants.inStock")!)
                        { Value = FieldValue.Boolean(true) })
                };

                filters.Add(Query.Bool(new BoolQuery
                {
                    Should = new Query[]
                    {
                        productAttr,
                        Query.Nested(new NestedQuery
                        {
                            Path = Field.FromString("variants")!,
                            Query = new BoolQuery { Must = nestedMust }
                        })
                    },
                    MinimumShouldMatch = 1
                }));
            }
        }

        var boolQuery = new BoolQuery { Filter = filters };
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            // Relevance: require all query terms to match the analyzed text fields (typo-tolerant via fuzziness),
            // OR all query n-grams to match the ngram subfield (substring search). The ngram subfield shares its
            // analyzer at query time, so without Operator.And a single shared 2-gram (e.g. "te") would match
            // unrelated products — hence the explicit AND on both clauses.
            boolQuery.Must = new[]
            {
                Query.Bool(new BoolQuery
                {
                    Should = new[]
                    {
                        Query.MultiMatch(new MultiMatchQuery
                        {
                            Query = kw,
                            Fields = new[] { "name.vi^3", "name.en^1", "slug^2" },
                            Type = TextQueryType.BestFields,
                            Operator = Operator.And,
                            Fuzziness = new Fuzziness("AUTO")
                        }),
                        Query.Match(new MatchQuery(Field.FromString("name.vi.ngram")!)
                        {
                            Query = kw,
                            Operator = Operator.And
                        })
                    },
                    MinimumShouldMatch = 1
                })
            };
        }

        var priceOrder = query.Sort == ProductSearchSort.PriceDesc ? SortOrder.Desc : SortOrder.Asc;
        var sort = new List<SortOptions>
        {
            SortOptions.Field(
                Field.FromString("minBasePrice.amount")!,
                new FieldSort { Order = priceOrder, Missing = FieldValue.String("_last") }),
            SortOptions.Field(Field.FromString("id")!, new FieldSort { Order = SortOrder.Asc })
        };

        var searchRequest = new SearchRequest(index)
        {
            Size = size,
            TrackTotalHits = new TrackHits(true),
            Query = boolQuery,
            Sort = sort,
            SearchAfter = searchAfter
        };

        if (query.IncludeFacets)
        {
            searchRequest.Aggregations = new Dictionary<string, Aggregation>
            {
                ["price_stats"] = Aggregation.Stats(new StatsAggregation { Field = Field.FromString("minBasePrice.amount")! }),
                ["categories"] = Aggregation.Terms(new TermsAggregation
                {
                    Field = Field.FromString("categoryAncestors")!,
                    Size = 20,
                    ExecutionHint = TermsAggregationExecutionHint.Map
                })
            };

            if (query.CustomFieldFacetProperties is { Count: > 0 } facetProps)
            {
                foreach (var property in facetProps)
                {
                    if (string.IsNullOrWhiteSpace(property) || property.Length > 64)
                        continue;
                    var aggKey = $"cf_{property.Replace(".", "_")}";
                    searchRequest.Aggregations[aggKey] = Aggregation.Terms(new TermsAggregation
                    {
                        Field = Field.FromString($"attributes.{property}")!,
                        Size = 20
                    });
                }
            }
        }

        SearchResponse<ProductDocument> response;
        try
        {
            response = await client.SearchAsync<ProductDocument>(searchRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Elasticsearch search failed for tenant {TenantId} store {StoreId}.", query.TenantId,
                query.StoreId);
            return new ProductSearchResult(Array.Empty<ProductSearchItem>(), 0, null);
        }

        if (!response.IsValidResponse)
        {
            logger.LogWarning("Elasticsearch search invalid: {Debug}", response.DebugInformation);
            return new ProductSearchResult(Array.Empty<ProductSearchItem>(), 0, null);
        }

        var total = response.Total;
        var items = new List<ProductSearchItem>();
        foreach (var hit in response.Hits)
        {
            if (hit.Source is null)
                continue;
            var src = hit.Source;
            var nameByLocale = src.Name.Count > 0
                ? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(src.Name, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            var display = nameByLocale.TryGetValue("vi", out var vi) && !string.IsNullOrWhiteSpace(vi)
                ? vi
                : nameByLocale.Values.FirstOrDefault(static s => !string.IsNullOrWhiteSpace(s)) ?? src.Slug;
            items.Add(new ProductSearchItem(src.Id, display, nameByLocale, src.MinBasePrice, src.HasInStockVariant,
                src.Slug, src.Status));
        }

        string? next = null;
        var last = response.Hits.LastOrDefault();
        if (last?.Sort is { Count: > 0 } sortValues && items.Count == size)
            next = EncodeSearchAfter(sortValues);

        var facets = query.IncludeFacets ? ParseFacets(response, query.CustomFieldFacetProperties) : null;
        return new ProductSearchResult(items, total, next, facets);
    }

    private static SearchFacets? ParseFacets(SearchResponse<ProductDocument> response,
        IReadOnlyList<string>? customFieldFacetProperties)
    {
        if (response.Aggregations is null || !response.Aggregations.Any())
            return new SearchFacets(Array.Empty<FacetTermBucket>(), new Dictionary<string, IReadOnlyList<FacetTermBucket>>(),
                null, null);

        long? pMin = null, pMax = null;
        if (response.Aggregations.TryGetValue("price_stats", out var psAgg) && psAgg is StatsAggregate stats)
        {
            if (stats.Min.HasValue)
                pMin = (long)stats.Min.Value;
            if (stats.Max.HasValue)
                pMax = (long)stats.Max.Value;
        }

        var catBuckets = new List<FacetTermBucket>();
        if (response.Aggregations.TryGetValue("categories", out var catAgg) && catAgg is LongTermsAggregate lTerms)
        {
            foreach (var b in lTerms.Buckets)
                catBuckets.Add(new FacetTermBucket(b.Key.ToString(CultureInfo.InvariantCulture), b.DocCount));
        }

        var attributeTerms = new Dictionary<string, IReadOnlyList<FacetTermBucket>>(StringComparer.Ordinal);
        if (customFieldFacetProperties is { Count: > 0 })
        {
            foreach (var property in customFieldFacetProperties)
            {
                if (string.IsNullOrWhiteSpace(property))
                    continue;
                var aggKey = $"cf_{property.Replace(".", "_")}";
                if (!response.Aggregations.TryGetValue(aggKey, out var agg))
                    continue;

                var buckets = new List<FacetTermBucket>();
                if (agg is StringTermsAggregate sTerms)
                {
                    foreach (var b in sTerms.Buckets)
                        buckets.Add(new FacetTermBucket(b.Key.ToString(), b.DocCount));
                }

                if (buckets.Count > 0)
                    attributeTerms[property] = buckets;
            }
        }

        return new SearchFacets(catBuckets, attributeTerms, pMin, pMax);
    }

    private static string? EncodeSearchAfter(IReadOnlyCollection<FieldValue> sort)
    {
        var list = new List<object?>(sort.Count);
        foreach (var v in sort)
            list.Add(FieldValueToObject(v));

        var json = JsonSerializer.Serialize(list, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static List<FieldValue>? DecodeSearchAfter(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Trim()));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<FieldValue>();
            foreach (var el in doc.RootElement.EnumerateArray())
                list.Add(JsonElementToFieldValue(el));
            return list;
        }
        catch
        {
            return null;
        }
    }

    private static object? FieldValueToObject(FieldValue v)
    {
        if (v.TryGetLong(out var l))
            return l;
        if (v.TryGetString(out var s))
            return s;
        if (v.TryGetDouble(out var d))
            return d;
        if (v.TryGetBool(out var b))
            return b;
        return null;
    }

    private static FieldValue JsonElementToFieldValue(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => FieldValue.String(el.GetString() ?? string.Empty),
            JsonValueKind.Number => el.TryGetInt64(out var l)
                ? FieldValue.Long(l)
                : FieldValue.Double(el.GetDouble()),
            JsonValueKind.True => FieldValue.Boolean(true),
            JsonValueKind.False => FieldValue.Boolean(false),
            _ => FieldValue.String(el.GetRawText())
        };
}
