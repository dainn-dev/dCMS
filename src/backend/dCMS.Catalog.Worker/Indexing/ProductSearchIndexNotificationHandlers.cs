using dCMS.Infrastructure.Search;
using MediatR;

namespace dCMS.Catalog.Worker.Indexing;

public sealed class UpsertProductSearchIndexNotificationHandler(ElasticsearchProductIndexer indexer)
    : INotificationHandler<UpsertProductSearchIndexNotification>
{
    public Task Handle(UpsertProductSearchIndexNotification notification, CancellationToken cancellationToken) =>
        indexer.IndexProductAsync(notification.TenantId, notification.StoreId, notification.ProductId,
            cancellationToken);
}

public sealed class DeleteProductSearchIndexNotificationHandler(ElasticsearchProductIndexer indexer)
    : INotificationHandler<DeleteProductSearchIndexNotification>
{
    public Task Handle(DeleteProductSearchIndexNotification notification, CancellationToken cancellationToken) =>
        indexer.DeleteProductAsync(notification.TenantId, notification.StoreId, notification.ProductId,
            cancellationToken);
}
