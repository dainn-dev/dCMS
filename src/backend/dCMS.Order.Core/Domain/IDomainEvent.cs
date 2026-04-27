namespace dCMS.Order.Core.Domain;

public interface IDomainEvent;

/// <summary>Warehouse line for saga <see cref="OrderPlaced"/> / <c>OrderPlacedV1</c> (US-19).</summary>
public sealed record OrderPlacedLine(string VariantId, string WarehouseId, int Quantity);

/// <summary>Sales-side line for analytics projection (Reports.Worker).</summary>
public sealed record OrderPlacedSalesLine(string ProductId, int Quantity, decimal LineTotal);

public sealed record OrderPlaced(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderPlacedLine> Lines,
    DateTimeOffset OccurredAt,
    IReadOnlyList<OrderPlacedSalesLine> SalesLines) : IDomainEvent;

public sealed record OrderConfirmed(string OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderShipped(string OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderDelivered(string OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderCancelled(string OrderId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
