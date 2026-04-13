using System.Text.Json;
using dCMS.Order.Core.Domain;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Infrastructure.Shipping;

public sealed class ShipmentWebhookProcessor
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _connectionString;
    private readonly ICarrierStatusMapper _statusMapper;

    public ShipmentWebhookProcessor(IConfiguration configuration, ICarrierStatusMapper statusMapper)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _statusMapper = statusMapper;
    }

    public async Task<ShipmentWebhookResult> ProcessAsync(
        string carrier,
        string trackingNumber,
        string externalStatus,
        DateTimeOffset occurredAt,
        string rawPayloadJson,
        CancellationToken cancellationToken = default)
    {
        var mapped = _statusMapper.Map(carrier, externalStatus);
        if (mapped == MappedStatus.Unknown)
            return ShipmentWebhookResult.InvalidStatus;

        var mappedDb = mapped.ToDbValue();
        var payload = JsonSerializer.Serialize(new
        {
            carrier,
            trackingNumber,
            externalStatus,
            mappedStatus = mappedDb,
            occurredAt,
            raw = TryParseJson(rawPayloadJson),
        }, Json);

        await using var uow = new OrderUnitOfWork(_connectionString);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var r = await uow.TryUpsertShipmentEventFromWebhookAsync(
                    carrier,
                    trackingNumber,
                    mappedDb,
                    occurredAt,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);

            if (r == ShipmentWebhookDbOutcome.NotFound)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ShipmentWebhookResult.UnknownTracking;
            }

            if (mapped == MappedStatus.Delivered)
            {
                // Trigger OrderDelivered flow (idempotent update Shipped->Delivered + outbox).
                var (tenantId, storeId, orderId) = await uow
                    .GetTenantStoreOrderIdByCarrierTrackingAsync(carrier, trackingNumber, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(orderId))
                {
                    await uow.TrySetOrderStatusAsync(
                            tenantId,
                            storeId,
                            orderId,
                            expectedCurrentStatus: nameof(OrderStatus.Shipped),
                            newStatus: nameof(OrderStatus.Delivered),
                            outboxIfUpdated: [new OrderDelivered(orderId, occurredAt)],
                            occurredAt: occurredAt,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ShipmentWebhookResult.Ok;
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static JsonElement TryParseJson(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(raw);
        }
        catch
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }
}

public enum ShipmentWebhookResult
{
    Ok = 0,
    UnknownTracking = 1,
    InvalidStatus = 2,
}

internal enum ShipmentWebhookDbOutcome
{
    Ok = 0,
    NotFound = 1,
}

