namespace dCMS.Core.Messaging;

/// <summary>US-22 / DAI-332 — emitted when a shipment is created for an order.</summary>
public sealed record OrderShippedV1(string OrderId, DateTimeOffset OccurredAt);

/// <summary>US-22 / DAI-330/336 — emitted when an order is marked delivered.</summary>
public sealed record OrderDeliveredV1(string OrderId, DateTimeOffset OccurredAt);
