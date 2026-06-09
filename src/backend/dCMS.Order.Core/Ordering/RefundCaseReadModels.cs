namespace dCMS.Order.Core.Ordering;

/// <summary>Keyset page of refund cases (DAI-653).</summary>
public sealed record RefundCasePage(IReadOnlyList<RefundCaseDetail> Items, string? NextCursor);

/// <summary>
/// Refund case read model: cancelled order + latest qualifying payment transaction (DAI-653).
/// </summary>
public sealed record RefundCaseDetail(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string PaymentProvider,
    string PaymentStatus,
    string PaymentIntentId,
    string? RefundCaseStatus,
    string RefundCaseRemark,
    DateTimeOffset? RefundedAt,
    DateTimeOffset? RefundCaseUpdatedAt,
    DateTimeOffset OrderCancelledAt,
    DateTimeOffset OrderCreatedAt);
