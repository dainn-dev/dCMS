using dCMS.Messaging.Contracts;

namespace dCMS.Core.Messaging;

// DAI-306 versioning: additive optional fields are OK within the same v1 contract; breaking changes require a new versioned type/topic (e.g. *.v2).

/// <summary>Inventory → Order (Linear DAI-306 / DAI-308; contract tests only until Order service consumes these).</summary>
public sealed record StockReservedV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record StockReservationFailedV1(string OrderId, string Reason, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record StockReleasedV1(string OrderId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>Order → Payment (Linear DAI-306 / DAI-308; contract tests only).</summary>
[MessageVersion("PaymentCompleted.v1")]
public sealed record PaymentCompletedV1(string OrderId, string PaymentId, string TenantId, string StoreId, DateTimeOffset OccurredAt);

public sealed record PaymentFailedV1(string OrderId, string Reason, string TenantId, string StoreId, DateTimeOffset OccurredAt);

/// <summary>Operational signal: order failed to progress and needs attention (DAI-631).</summary>
[MessageVersion("OrderFailed.v1")]
public sealed record OrderFailedV1(
    string OrderId,
    string TenantId,
    string StoreId,
    string FailureStatus,
    string FailureReason,
    string? FailureErrorCode,
    DateTimeOffset FailedAt);

/// <summary>Order → Payment (US-F3 / DAI-356): refund after late <see cref="PaymentCompletedV1"/> on a cancelled order.</summary>
public sealed record RefundPaymentV1(
    string OrderId,
    string TenantId,
    string StoreId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTimeOffset RequestedAt);

/// <summary>Payment → Order saga: refund completed at provider (idempotent re-publish allowed).</summary>
public sealed record PaymentRefundedV1(
    string OrderId,
    string RefundId,
    decimal Amount,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt);
