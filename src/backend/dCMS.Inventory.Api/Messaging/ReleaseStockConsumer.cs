using dCMS.Core.Messaging;
using dCMS.Inventory.Commands;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Inventory.Api.Messaging;

/// <summary>DAI-318 — consumes <see cref="ReleaseStockV1"/> from Order saga compensation; releases reserved stock.</summary>
public sealed class ReleaseStockConsumer(
    StockService stockService,
    ILogger<ReleaseStockConsumer> logger) : IConsumer<ReleaseStockV1>
{
    public async Task Consume(ConsumeContext<ReleaseStockV1> context)
    {
        var m = context.Message;
        var at = DateTimeOffset.UtcNow;
        var createdBy = $"order-release:{m.OrderId}";

        if (m.Lines is null || m.Lines.Count == 0)
        {
            await context
                .Publish(new StockReleasedV1(m.OrderId, m.TenantId, m.StoreId, at), context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var line in m.Lines)
        {
            if (line.Quantity <= 0)
                continue;

            try
            {
                await stockService
                    .ReleaseStockAsync(
                        new ReleaseStockCommand(
                            m.TenantId,
                            m.StoreId,
                            line.VariantId,
                            line.WarehouseId,
                            line.Quantity,
                            createdBy,
                            m.OrderId),
                        at,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (VariantStockNotFoundException ex)
            {
                logger.LogWarning(
                    ex,
                    "ReleaseStockV1 skipped missing stock row for order {OrderId} variant {VariantId}",
                    m.OrderId,
                    line.VariantId);
            }
        }

        await context
            .Publish(new StockReleasedV1(m.OrderId, m.TenantId, m.StoreId, at), context.CancellationToken)
            .ConfigureAwait(false);
    }
}
