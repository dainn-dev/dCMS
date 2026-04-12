namespace dCMS.Core.Messaging;

// DAI-306 versioning: additive optional fields are OK within the same v1 contract; breaking changes require a new versioned type/topic (e.g. *.v2).

/// <summary>Inventory → Order (Linear DAI-306 / DAI-308; contract tests only until Order service consumes these).</summary>
public sealed record StockReservedV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record StockReservationFailedV1(string OrderId, string Reason, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record StockReleasedV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>Order → Payment (Linear DAI-306 / DAI-308; contract tests only).</summary>
public sealed record PaymentCompletedV1(string OrderId, string PaymentId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record PaymentFailedV1(string OrderId, string Reason, string TenantId, string StoreId, DateTimeOffset OccurredAt);
