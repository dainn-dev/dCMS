using System.Text.Json;
using dCMS.Order.Core.Domain;

namespace dCMS.Order.Infrastructure.Persistence;

internal static class OrderOutboxSerializer
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static (string EventType, string PayloadJson) ToOutboxRow(IDomainEvent e) =>
        e switch
        {
            OrderPlaced x => (
                "OrderPlaced",
                JsonSerializer.Serialize(
                    new
                    {
                        x.OrderId,
                        x.TenantId,
                        x.StoreId,
                        x.CustomerId,
                        totalAmount = x.TotalAmount,
                        x.Currency,
                        lines = x.Lines.Select(l => new { l.VariantId, l.WarehouseId, l.Quantity }),
                        x.OccurredAt,
                    },
                    Json)),
            OrderConfirmed x => ("OrderConfirmed", JsonSerializer.Serialize(new { x.OrderId, x.OccurredAt }, Json)),
            OrderShipped x => ("OrderShipped", JsonSerializer.Serialize(new { x.OrderId, x.OccurredAt }, Json)),
            OrderDelivered x => ("OrderDelivered", JsonSerializer.Serialize(new { x.OrderId, x.OccurredAt }, Json)),
            OrderCancelled x => ("OrderCancelled", JsonSerializer.Serialize(new { x.OrderId, x.Reason, x.OccurredAt }, Json)),
            OrderFailed x => (
                "OrderFailed",
                JsonSerializer.Serialize(
                    new
                    {
                        x.OrderId,
                        x.TenantId,
                        x.StoreId,
                        x.FailureStatus,
                        x.FailureReason,
                        x.FailureErrorCode,
                        failedAt = x.FailedAt
                    },
                    Json)),
            ProductRestocked x => (
                "ProductRestocked.v1",
                JsonSerializer.Serialize(
                    new
                    {
                        x.OrderId,
                        x.TenantId,
                        x.StoreId,
                        x.VariantId,
                        x.Quantity,
                        x.ReturnId,
                        x.OccurredAt,
                    },
                    Json)),
            ReturnStatusChanged x => (
                "ReturnStatusChanged.v1",
                JsonSerializer.Serialize(
                    new
                    {
                        x.ReturnId,
                        x.OrderId,
                        x.FromStatus,
                        x.ToStatus,
                        x.OccurredAt,
                    },
                    Json)),
            _ => throw new InvalidOperationException($"Unknown order domain event {e.GetType().Name}."),
        };
}
