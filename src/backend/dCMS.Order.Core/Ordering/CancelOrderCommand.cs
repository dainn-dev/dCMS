namespace dCMS.Order.Core.Ordering;

/// <summary>US-21 / DAI-326 — <c>POST /api/orders/{id}/cancel</c>.</summary>
public sealed record CancelOrderCommand(
    string TenantId,
    string StoreId,
    string OrderId,
    string IdempotencyKey,
    /// <summary>Optional; when set, must match the order's <c>CustomerId</c> (DAI-328 RBAC).</summary>
    string? CallerCustomerId,
    string Reason,
    DateTimeOffset OccurredAt);
