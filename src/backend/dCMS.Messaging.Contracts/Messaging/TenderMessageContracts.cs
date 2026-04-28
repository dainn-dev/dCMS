namespace dCMS.Core.Messaging;

// DAI-723 — events emitted by Voucher.Api / Loyalty.Api as part of the multi-tender
// payment orchestration (DAI-689). Saga (DAI-724) will consume these to advance/compensate
// per-component state on the OrderPayment aggregate.

/// <summary>Voucher reserved for an order — funds are held until 'ExpiresAt' (15-min TTL by default).</summary>
public sealed record VoucherReservedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    string Code,
    decimal Amount,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredAt);

/// <summary>Voucher hold captured (committed) — balance is now consumed.</summary>
public sealed record VoucherCapturedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    Guid VoucherId,
    decimal Amount,
    DateTimeOffset OccurredAt);

/// <summary>Voucher hold released (rolled back) before capture — balance returned.</summary>
public sealed record VoucherReleasedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    Guid VoucherId,
    decimal Amount,
    string Reason,
    DateTimeOffset OccurredAt);

/// <summary>Voucher refunded after capture — balance restored.</summary>
public sealed record VoucherRefundedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    Guid VoucherId,
    decimal Amount,
    DateTimeOffset OccurredAt);

/// <summary>Loyalty points reserved for an order — held against the customer's ledger until capture/release.</summary>
public sealed record LoyaltyReservedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredAt);

/// <summary>Loyalty hold captured — points debited via ledger entry.</summary>
public sealed record LoyaltyCapturedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset OccurredAt);

/// <summary>Loyalty hold released — funds returned to balance (no ledger entry needed beyond reversal).</summary>
public sealed record LoyaltyReleasedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    string CustomerId,
    decimal Amount,
    string Reason,
    DateTimeOffset OccurredAt);

/// <summary>Loyalty refunded after capture — credit ledger entry reverses the debit.</summary>
public sealed record LoyaltyRefundedV1(
    string TenantId,
    Guid OrderId,
    Guid HoldId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset OccurredAt);

/// <summary>
/// DAI-724: late-cancel compensation. Saga publishes this when an order is cancelled after one or more
/// payment components have already captured (e.g. ProcessPaymentV1 succeeded then the order was cancelled).
/// Consumed by the multi-tender PaymentOrchestrator which issues per-component refund/release calls
/// idempotently keyed by (OrderId, ComponentId, Action).
/// </summary>
public sealed record ReleasePaymentComponentsV1(
    Guid OrderId,
    string TenantId,
    string StoreId,
    string Reason,
    DateTimeOffset RequestedAt);
