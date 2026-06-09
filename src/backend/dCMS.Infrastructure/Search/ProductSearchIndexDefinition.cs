using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;

namespace dCMS.Infrastructure.Search;

/// <summary>Creates product search **physical** index with analyzers + mappings (US-7 / US-8).</summary>
public static class ProductSearchIndexDefinition
{
    /// <summary>Creates the index when missing; no-op if it already exists.</summary>
    public static async Task CreateIndexIfNotExistsAsync(ElasticsearchClient client, string index,
        CancellationToken cancellationToken = default)
    {
        var exists = await client.Indices.ExistsAsync(index, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (exists.Exists)
            return;

        IndexName idx = index;
        await client.Indices.CreateAsync(idx, c => c
            .Settings(s => s.OtherSettings(o => o
                .Add("analysis", new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["tokenizer"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["ngram_tokenizer"] = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["type"] = "ngram",
                            ["min_gram"] = 2,
                            ["max_gram"] = 3
                        }
                    },
                    ["analyzer"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["ngram_analyzer"] = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["type"] = "custom",
                            ["tokenizer"] = "ngram_tokenizer",
                            ["filter"] = new[] { "lowercase" }
                        }
                    }
                })))
            .Mappings(m => m
                .Properties(new Properties
                {
                    { "id", new KeywordProperty() },
                    { "tenantId", new KeywordProperty() },
                    { "storeId", new KeywordProperty() },
                    { "brandId", new KeywordProperty() },
                    { "categoryId", new KeywordProperty() },
                    { "categoryPath", new TextProperty() },
                    { "categoryAncestors", new IntegerNumberProperty() },
                    {
                        "name", new ObjectProperty
                        {
                            Properties = new Properties
                            {
                                {
                                    "vi", new TextProperty
                                    {
                                        Analyzer = "standard",
                                        Fields = new Properties
                                        {
                                            {
                                                "ngram", new TextProperty { Analyzer = "ngram_analyzer" }
                                            }
                                        }
                                    }
                                },
                                { "en", new TextProperty { Analyzer = "standard" } }
                            }
                        }
                    },
                    { "slug", new KeywordProperty() },
                    { "status", new KeywordProperty() },
                    { "storeCurrency", new KeywordProperty() },
                    { "salesCount30d", new IntegerNumberProperty() },
                    { "attributes", new FlattenedProperty() },
                    {
                        "variants", new NestedProperty
                        {
                            Properties = new Properties
                            {
                                { "variantId", new KeywordProperty() },
                                { "sku", new KeywordProperty() },
                                { "status", new KeywordProperty() },
                                { "inStock", new BooleanProperty() },
                                { "availableQty", new IntegerNumberProperty() },
                                {
                                    "basePrice", new ObjectProperty
                                    {
                                        Properties = new Properties
                                        {
                                            { "amount", new LongNumberProperty() },
                                            { "currency", new KeywordProperty() }
                                        }
                                    }
                                },
                                { "attributes", new FlattenedProperty() }
                            }
                        }
                    },
                    { "hasInStockVariant", new BooleanProperty() },
                    { "totalAvailableQty", new IntegerNumberProperty() },
                    {
                        "minBasePrice", new ObjectProperty
                        {
                            Properties = new Properties
                            {
                                { "amount", new LongNumberProperty() },
                                { "currency", new KeywordProperty() }
                            }
                        }
                    },
                    { "snapshotVersion", new IntegerNumberProperty() },
                    { "updatedAt", new DateProperty() }
                })), cancellationToken).ConfigureAwait(false);
    }
}
