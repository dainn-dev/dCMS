using System.Text.Json;
using dCMS.Core.Events;

namespace dCMS.Infrastructure.Catalog;

internal static class DomainEventSerializer
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static (string EventType, string PayloadJson) ToOutboxRow(IDomainEvent e) =>
        e switch
        {
            ProductCreated x => ("ProductCreated", JsonSerializer.Serialize(new { x.ProductId, x.TenantId, x.StoreId, x.OccurredAt }, Options)),
            ProductUpdated x => ("ProductUpdated", JsonSerializer.Serialize(new { x.ProductId, x.TenantId, x.StoreId, x.OccurredAt }, Options)),
            ProductPublished x => ("ProductPublished", JsonSerializer.Serialize(new { x.ProductId, x.TenantId, x.StoreId, x.OccurredAt }, Options)),
            ProductArchived x => ("ProductArchived", JsonSerializer.Serialize(new { x.ProductId, x.TenantId, x.StoreId, x.OccurredAt }, Options)),
            _ => throw new InvalidOperationException($"Unknown domain event type {e.GetType().Name}.")
        };
}
