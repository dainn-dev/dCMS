using dCMS.Core.Search;
using dCMS.Infrastructure.Search;
using Elastic.Clients.Elasticsearch;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.Elasticsearch;
using Xunit;

namespace dCMS.Tests.Integration.Search;

public sealed class ProductSearchElasticsearchIntegrationTests : IAsyncLifetime
{
    private ElasticsearchContainer? _container;
    private bool _dockerReady;

    public async Task InitializeAsync()
    {
        _dockerReady = false;
        try
        {
            _container = new ElasticsearchBuilder().Build();
            await _container.StartAsync();
            _dockerReady = true;
        }
        catch
        {
            _dockerReady = false;
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    [SkippableFact]
    public async Task Search_keyword_filters_facets_and_sort_roundtrip()
    {
        Skip.IfNot(_dockerReady, "Docker / Testcontainers not available.");

        var uri = new Uri(_container!.GetConnectionString());
        var factory = new ElasticsearchClientFactory(uri,
            s => s.ServerCertificateValidationCallback((_, _, _, _) => true));
        var client = factory.Client;
        const string tenantId = "t1";
        const string storeId = "s1";
        var index = ElasticsearchIndexNames.Products(tenantId);
        await ProductSearchIndexAliasBootstrap.EnsureForTenantAsync(client, tenantId, null, CancellationToken.None);

        var doc = new ProductDocument
        {
            Id = "p1",
            TenantId = tenantId,
            StoreId = storeId,
            BrandId = "b1",
            CategoryId = "10",
            CategoryPath = "/1/10",
            CategoryAncestors = new[] { 1, 10 },
            Name = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vi"] = "Laptop ABC",
                ["en"] = "Laptop ABC EN"
            },
            Slug = "laptop-abc",
            Status = "active",
            StoreCurrency = "VND",
            SalesCount30d = 0,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
            Variants = new[]
            {
                new VariantDocument
                {
                    VariantId = "v1",
                    Sku = "sku-1",
                    Status = "active",
                    InStock = true,
                    AvailableQty = 3,
                    BasePrice = new MoneyAmount(1_000_000, "VND"),
                    Attributes = new Dictionary<string, object>(StringComparer.Ordinal) { ["color"] = "red" }
                }
            },
            HasInStockVariant = true,
            MinBasePrice = new MoneyAmount(1_000_000, "VND"),
            SnapshotVersion = 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        IndexName idx = index;
        Id id = doc.Id;
        var indexResp = await client.IndexAsync(doc, idx, id, cancellationToken: CancellationToken.None);
        indexResp.IsValidResponse.Should().BeTrue(indexResp.DebugInformation);
        var refreshResp = await client.Indices.RefreshAsync(idx, cancellationToken: CancellationToken.None);
        refreshResp.IsValidResponse.Should().BeTrue(refreshResp.DebugInformation);

        var search = new ElasticsearchProductSearchService(factory, NullLogger<ElasticsearchProductSearchService>.Instance);

        var matchAll = await search.SearchAsync(
            new ProductSearchQuery(tenantId, storeId, null, null, null, 20, null), CancellationToken.None);
        matchAll.TotalCount.Should().Be(1, "document should match store/tenant/status filters");
        matchAll.Items.Should().ContainSingle();

        var byKeyword = await search.SearchAsync(
            new ProductSearchQuery(tenantId, storeId, "Laptop", null, null, 20, null), CancellationToken.None);
        byKeyword.TotalCount.Should().Be(1);
        byKeyword.Items.Should().ContainSingle();
        byKeyword.Items[0].Id.Should().Be("p1");

        var byAttr = await search.SearchAsync(
            new ProductSearchQuery(tenantId, storeId, null, null, null, 20, null, null, null, ProductSearchSort.PriceAsc,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["color"] = "red" }), CancellationToken.None);
        byAttr.Items.Should().ContainSingle();

        var withFacets = await search.SearchAsync(
            new ProductSearchQuery(tenantId, storeId, null, null, null, 20, null, null, null, ProductSearchSort.PriceAsc,
                null, IncludeFacets: true), CancellationToken.None);
        withFacets.Facets.Should().NotBeNull();
        withFacets.Facets!.CategoryAncestors.Should().NotBeEmpty();
        withFacets.Facets.PriceMin.Should().NotBeNull();
        withFacets.Facets.PriceMax.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task Legacy_concrete_index_migrates_to_alias_and_backing_without_losing_documents()
    {
        Skip.IfNot(_dockerReady, "Docker / Testcontainers not available.");

        var uri = new Uri(_container!.GetConnectionString());
        var factory = new ElasticsearchClientFactory(uri,
            s => s.ServerCertificateValidationCallback((_, _, _, _) => true));
        var client = factory.Client;
        const string tenantId = "migrate1";
        var logical = ElasticsearchIndexNames.Products(tenantId);
        var backing = ElasticsearchIndexNames.ProductsBackingIndex(tenantId, ProductSearchIndexVersion.Latest);

        await ProductSearchIndexDefinition.CreateIndexIfNotExistsAsync(client, logical, CancellationToken.None);

        var doc = new ProductDocument
        {
            Id = "legacy-p1",
            TenantId = tenantId,
            StoreId = "s1",
            BrandId = "b1",
            CategoryId = "1",
            CategoryPath = "/1",
            CategoryAncestors = new[] { 1 },
            Name = new Dictionary<string, string>(StringComparer.Ordinal) { ["vi"] = "Migrated" },
            Slug = "migrated",
            Status = "active",
            StoreCurrency = "VND",
            SalesCount30d = 0,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
            Variants = Array.Empty<VariantDocument>(),
            HasInStockVariant = false,
            MinBasePrice = new MoneyAmount(1, "VND"),
            SnapshotVersion = 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        IndexName idx = logical;
        Id id = doc.Id;
        var legIdx = await client.IndexAsync(doc, idx, id, cancellationToken: CancellationToken.None);
        legIdx.IsValidResponse.Should().BeTrue(legIdx.DebugInformation);
        await client.Indices.RefreshAsync(idx, cancellationToken: CancellationToken.None);

        await ProductSearchIndexAliasBootstrap.EnsureForTenantAsync(client, tenantId, null, CancellationToken.None);

        var aliasOk = await client.Indices.ExistsAliasAsync(Names.Parse(logical), CancellationToken.None);
        aliasOk.Exists.Should().BeTrue();

        var search = new ElasticsearchProductSearchService(factory, NullLogger<ElasticsearchProductSearchService>.Instance);
        var found = await search.SearchAsync(
            new ProductSearchQuery(tenantId, "s1", "Migrated", null, null, 20, null), CancellationToken.None);
        found.Items.Should().ContainSingle(i => i.Id == "legacy-p1");

        (await client.Indices.ExistsAsync(logical, CancellationToken.None)).Exists.Should().BeTrue();
        (await client.Indices.ExistsAsync(backing, CancellationToken.None)).Exists.Should().BeTrue();
    }
}
