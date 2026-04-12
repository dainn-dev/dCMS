using dCMS.Catalog.Worker.Indexing;
using dCMS.Core.Messaging;
using MassTransit;
using MediatR;

namespace dCMS.Catalog.Worker.Consumers;

public sealed class ProductCreatedIndexConsumer(IMediator mediator) : IConsumer<ProductCreatedV1>
{
    public Task Consume(ConsumeContext<ProductCreatedV1> context) =>
        mediator.Publish(
            new UpsertProductSearchIndexNotification(context.Message.ProductId, context.Message.TenantId,
                context.Message.StoreId), context.CancellationToken);
}

public sealed class ProductUpdatedIndexConsumer(IMediator mediator) : IConsumer<ProductUpdatedV1>
{
    public Task Consume(ConsumeContext<ProductUpdatedV1> context) =>
        mediator.Publish(
            new UpsertProductSearchIndexNotification(context.Message.ProductId, context.Message.TenantId,
                context.Message.StoreId), context.CancellationToken);
}

public sealed class ProductPublishedIndexConsumer(IMediator mediator) : IConsumer<ProductPublishedV1>
{
    public Task Consume(ConsumeContext<ProductPublishedV1> context) =>
        mediator.Publish(
            new UpsertProductSearchIndexNotification(context.Message.ProductId, context.Message.TenantId,
                context.Message.StoreId), context.CancellationToken);
}

public sealed class ProductArchivedIndexConsumer(IMediator mediator) : IConsumer<ProductArchivedV1>
{
    public Task Consume(ConsumeContext<ProductArchivedV1> context) =>
        mediator.Publish(
            new DeleteProductSearchIndexNotification(context.Message.ProductId, context.Message.TenantId,
                context.Message.StoreId), context.CancellationToken);
}

public sealed class StockUpdatedIndexConsumer(
    DebouncedStockProductIndexPublisher debouncer,
    dCMS.Core.Persistence.ICatalogPersistence catalog) : IConsumer<StockUpdatedV1>
{
    public async Task Consume(ConsumeContext<StockUpdatedV1> context)
    {
        var m = context.Message;
        var productId = await catalog
            .GetProductIdByVariantIdAsync(m.VariantId, m.TenantId, m.StoreId, context.CancellationToken)
            .ConfigureAwait(false);
        if (productId is null)
            return;
        debouncer.ScheduleUpsert(m.TenantId, m.StoreId, productId);
    }
}
