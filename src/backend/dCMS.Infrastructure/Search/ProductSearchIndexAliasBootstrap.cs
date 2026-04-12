using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Reindex;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Search;

/// <summary>US-8: versioned backing index + stable alias (<see cref="ElasticsearchIndexNames.Products"/>). Greenfield creates <c>-v{N}</c> + alias; legacy concrete index with the same name is reindexed then swapped.</summary>
public static class ProductSearchIndexAliasBootstrap
{
    /// <summary>
    /// Ensures <see cref="ElasticsearchIndexNames.Products"/> exists as an alias to <c>…-v<see cref="ProductSearchIndexVersion.Latest"/></c>.
    /// Migrating from a pre-US-8 **index** with the alias name causes a short window where that name may not resolve between delete and alias creation; search/index should retry.
    /// </summary>
    public static async Task EnsureForTenantAsync(ElasticsearchClient client, string tenantId,
        ILogger? logger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var alias = ElasticsearchIndexNames.ProductsAlias(tenantId);
        var backing = ElasticsearchIndexNames.ProductsBackingIndex(tenantId, ProductSearchIndexVersion.Latest);

        var aliasHead = await client.Indices.ExistsAliasAsync(Names.Parse(alias), cancellationToken).ConfigureAwait(false);
        if (aliasHead is { IsValidResponse: true, Exists: true })
            return;

        if (!aliasHead.IsValidResponse)
            logger?.LogWarning("ExistsAlias check invalid for {Alias}: {Debug}", alias, aliasHead.DebugInformation);

        var legacyExists = await client.Indices.ExistsAsync(alias, cancellationToken).ConfigureAwait(false);
        var backingExists = await client.Indices.ExistsAsync(backing, cancellationToken).ConfigureAwait(false);

        if (!legacyExists.Exists && backingExists.Exists)
        {
            await PutWriteAliasAsync(client, backing, alias, cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("Attached product search alias {Alias} to existing backing {Backing}.", alias, backing);
            return;
        }

        if (legacyExists.Exists && backingExists.Exists)
        {
            logger?.LogWarning(
                "Both legacy index {Alias} and backing {Backing} exist; dropping backing and re-running migration.",
                alias, backing);
            await client.Indices.DeleteAsync(backing, cancellationToken: cancellationToken).ConfigureAwait(false);
            backingExists = await client.Indices.ExistsAsync(backing, cancellationToken).ConfigureAwait(false);
        }

        if (!legacyExists.Exists && !backingExists.Exists)
        {
            await ProductSearchIndexDefinition.CreateIndexIfNotExistsAsync(client, backing, cancellationToken)
                .ConfigureAwait(false);
            await PutWriteAliasAsync(client, backing, alias, cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("Created product search backing {Backing} and alias {Alias}.", backing, alias);
            return;
        }

        if (legacyExists.Exists)
        {
            await ProductSearchIndexDefinition.CreateIndexIfNotExistsAsync(client, backing, cancellationToken)
                .ConfigureAwait(false);

            var reindex = await client.ReindexAsync(new ReindexRequest
            {
                Source = new Source { Indices = alias },
                Dest = new Destination { Index = backing },
                WaitForCompletion = true,
                Refresh = true,
                Conflicts = Conflicts.Proceed,
                Timeout = TimeSpan.FromMinutes(10)
            }, cancellationToken).ConfigureAwait(false);

            if (!reindex.IsValidResponse || (reindex.Failures?.Count ?? 0) > 0)
            {
                logger?.LogError("Reindex {Source} -> {Dest} failed: {Debug}", alias, backing, reindex.DebugInformation);
                throw new InvalidOperationException(
                    $"Elasticsearch reindex from '{alias}' to '{backing}' failed. See logs for DebugInformation.");
            }

            await client.Indices.DeleteAsync(alias, cancellationToken: cancellationToken).ConfigureAwait(false);
            await PutWriteAliasAsync(client, backing, alias, cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("Migrated product search from legacy index {Alias} to {Backing} with alias.", alias,
                backing);
        }
    }

    private static Task PutWriteAliasAsync(ElasticsearchClient client, string backingIndex, string aliasName,
        CancellationToken cancellationToken) =>
        client.Indices.PutAliasAsync(new PutAliasRequest(backingIndex, aliasName) { IsWriteIndex = true },
            cancellationToken);
}
