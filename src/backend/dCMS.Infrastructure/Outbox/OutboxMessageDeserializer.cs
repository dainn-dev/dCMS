using System.Text.Json;
using dCMS.Core.Messaging;

namespace dCMS.Infrastructure.Outbox;

public static class OutboxMessageDeserializer
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Maps outbox row to MassTransit message contract. Returns null for unknown types.</summary>
    public static object? Deserialize(string eventType, string payloadJson) =>
        eventType switch
        {
            "ProductCreated" => JsonSerializer.Deserialize<ProductCreatedV1>(payloadJson, Options),
            "ProductUpdated" => JsonSerializer.Deserialize<ProductUpdatedV1>(payloadJson, Options),
            "ProductPublished" => JsonSerializer.Deserialize<ProductPublishedV1>(payloadJson, Options),
            "ProductArchived" => JsonSerializer.Deserialize<ProductArchivedV1>(payloadJson, Options),
            "StockUpdated.v1" => JsonSerializer.Deserialize<StockUpdatedV1>(payloadJson, Options),
            "OrderPlaced" => DeserializeOrderPlacedV1(payloadJson),
            "OrderShipped" => JsonSerializer.Deserialize<OrderShippedV1>(payloadJson, Options),
            "OrderDelivered" => JsonSerializer.Deserialize<OrderDeliveredV1>(payloadJson, Options),
            _ => null
        };

    private static OrderPlacedV1? DeserializeOrderPlacedV1(string payloadJson)
    {
        var dto = JsonSerializer.Deserialize<OrderPlacedOutboxDto>(payloadJson, Options);
        if (dto is null || string.IsNullOrWhiteSpace(dto.OrderId))
            return null;

        var lines = dto.Lines ?? [];
        var v1Lines = lines
            .Select(l => new OrderPlacedLineV1(l.VariantId ?? "", l.WarehouseId ?? "", l.Quantity))
            .ToList();

        return new OrderPlacedV1(
            dto.OrderId,
            dto.TenantId ?? "",
            dto.StoreId ?? "",
            dto.CustomerId ?? "",
            dto.TotalAmount,
            dto.Currency ?? "USD",
            v1Lines,
            dto.OccurredAt);
    }

    private sealed class OrderPlacedOutboxDto
    {
        public string? OrderId { get; set; }
        public string? TenantId { get; set; }
        public string? StoreId { get; set; }
        public string? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Currency { get; set; }
        public List<OrderPlacedLineOutboxDto>? Lines { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
    }

    private sealed class OrderPlacedLineOutboxDto
    {
        public string? VariantId { get; set; }
        public string? WarehouseId { get; set; }
        public int Quantity { get; set; }
    }
}
