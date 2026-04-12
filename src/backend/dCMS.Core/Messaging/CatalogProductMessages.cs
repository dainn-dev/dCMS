namespace dCMS.Core.Messaging;

// DAI-306 versioning: additive optional fields OK within the same v1 contract; breaking changes require a new versioned type/topic (e.g. *.v2).

/// <summary>Contracts published from Catalog outbox → RabbitMQ (MassTransit). JSON matches DomainEventSerializer outbox payloads.</summary>
public sealed record ProductCreatedV1(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record ProductUpdatedV1(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record ProductPublishedV1(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record ProductArchivedV1(string ProductId, string TenantId, string StoreId, DateTimeOffset OccurredAt);
