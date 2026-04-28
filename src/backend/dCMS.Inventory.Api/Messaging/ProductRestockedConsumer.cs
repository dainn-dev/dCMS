using dCMS.Core.Messaging;
using dCMS.Inventory.Commands;
using dCMS.Inventory.Models;
using dCMS.Inventory.Persistence;
using dCMS.Inventory.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Inventory.Api.Messaging;

/// <summary>DAI-727 — Inventory subscribes to <see cref="ProductRestockedV1"/> emitted by Order
/// when an approved Return restocks units. Idempotency is handled upstream by
/// <c>MessageIdempotencyConsumeFilter</c> (stable outbox MessageId, ProcessedMessages table).</summary>
public sealed class ProductRestockedConsumer(
    IInventoryStockPersistence persistence,
    StockService stockService,
    ILogger<ProductRestockedConsumer> logger) : IConsumer<ProductRestockedV1>
{
    public async Task Consume(ConsumeContext<ProductRestockedV1> context)
    {
        var m = context.Message;
        if (m.Quantity <= 0)
        {
            logger.LogWarning("ProductRestockedV1 ignored: non-positive quantity {Qty} for return {ReturnId}.",
                m.Quantity, m.ReturnId);
            return;
        }

        // Order does not track per-line WarehouseId on returns; resolve via existing stock rows for the variant.
        var stockRows = await persistence
            .ListStockByVariantAsync(m.TenantId, m.StoreId, m.VariantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (stockRows.Count == 0)
        {
            logger.LogWarning(
                "ProductRestockedV1 dropped: no warehouse stock row for variant {VariantId} in tenant {TenantId} store {StoreId} (return {ReturnId}).",
                m.VariantId, m.TenantId, m.StoreId, m.ReturnId);
            return;
        }

        var warehouseId = stockRows.OrderBy(r => r.WarehouseId).First().WarehouseId;

        var command = new AdjustStockCommand(
            m.TenantId,
            m.StoreId,
            m.VariantId,
            warehouseId,
            m.Quantity,
            CreatedBy: $"return:{m.ReturnId}",
            ReferenceId: m.ReturnId,
            MovementType: StockMovementType.Return);

        await stockService.AdjustStockAsync(command, m.OccurredAt, context.CancellationToken).ConfigureAwait(false);
    }
}
