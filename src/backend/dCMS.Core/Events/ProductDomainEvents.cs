namespace dCMS.Core.Events;

public sealed record ProductCreated(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProductUpdated(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProductPublished(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProductArchived(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt) : IDomainEvent;
