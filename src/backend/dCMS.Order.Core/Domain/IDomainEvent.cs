namespace dCMS.Order.Core.Domain;

public interface IDomainEvent;

public sealed record OrderPlaced(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderConfirmed(string OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderShipped(string OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderCancelled(string OrderId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
