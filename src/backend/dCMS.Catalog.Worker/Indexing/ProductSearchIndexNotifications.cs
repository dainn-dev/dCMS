using MediatR;

namespace dCMS.Catalog.Worker.Indexing;

public sealed record UpsertProductSearchIndexNotification(string ProductId, string TenantId, string StoreId)
    : INotification;

public sealed record DeleteProductSearchIndexNotification(string ProductId, string TenantId, string StoreId)
    : INotification;
