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
            _ => null
        };
}
