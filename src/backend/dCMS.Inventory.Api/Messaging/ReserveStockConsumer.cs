using dCMS.Core.Exceptions;
using dCMS.Core.Messaging;
using dCMS.Inventory.Commands;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Inventory.Api.Messaging;

/// <summary>DAI-318 — consumes <see cref="ReserveStockV1"/> from Order saga; reserves stock and publishes terminal events.</summary>
public sealed class ReserveStockConsumer(
    StockService stockService,
    ILogger<ReserveStockConsumer> logger) : IConsumer<ReserveStockV1>
{
    public async Task Consume(ConsumeContext<ReserveStockV1> context)
    {
        var m = context.Message;
        var at = DateTimeOffset.UtcNow;
        var createdBy = $"order:{m.OrderId}";

        if (m.Lines is null || m.Lines.Count == 0)
        {
            await context.Publish(
                    new StockReservationFailedV1(m.OrderId, "no_lines", m.TenantId, m.StoreId, at),
                    context.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var line in m.Lines)
        {
            if (line.Quantity <= 0)
            {
                await PublishFailedAsync(context, m, "invalid_quantity", at).ConfigureAwait(false);
                return;
            }

            try
            {
                await stockService
                    .ReserveStockAsync(
                        new ReserveStockCommand(
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
            catch (OutOfStockException ex)
            {
                logger.LogWarning(
                    ex,
                    "ReserveStockV1 failed for order {OrderId} variant {VariantId}: insufficient stock",
                    m.OrderId,
                    line.VariantId);
                await PublishFailedAsync(context, m, "insufficient_stock", at).ConfigureAwait(false);
                return;
            }
            catch (VariantStockNotFoundException ex)
            {
                logger.LogWarning(
                    ex,
                    "ReserveStockV1 failed for order {OrderId} variant {VariantId}: stock row missing",
                    m.OrderId,
                    line.VariantId);
                await PublishFailedAsync(context, m, "stock_row_missing", at).ConfigureAwait(false);
                return;
            }
        }

        await context
            .Publish(new StockReservedV1(m.OrderId, m.TenantId, m.StoreId, at), context.CancellationToken)
            .ConfigureAwait(false);
    }

    private static Task PublishFailedAsync(ConsumeContext<ReserveStockV1> context, ReserveStockV1 m, string reason, DateTimeOffset at) =>
        context.Publish(
            new StockReservationFailedV1(m.OrderId, reason, m.TenantId, m.StoreId, at),
            context.CancellationToken);
}
