using Elastic.Clients.Elasticsearch;

namespace dCMS.Infrastructure.Search;

/// <summary>Single shared <see cref="ElasticsearchClient"/> for the worker (DAI-238 factory).</summary>
public sealed class ElasticsearchClientFactory
{
    public ElasticsearchClientFactory(Uri elasticsearchUri, Action<ElasticsearchClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(elasticsearchUri);
        var settings = new ElasticsearchClientSettings(elasticsearchUri);
        configureSettings?.Invoke(settings);
        Client = new ElasticsearchClient(settings);
    }

    public ElasticsearchClient Client { get; }
}
