using dCMS.Loyalty.Api.Domain;

namespace dCMS.Loyalty.Api.Persistence;

public interface ILoyaltyStore
{
    /// <summary>Returns the customer's available-to-spend balance: sum of ledger Delta minus the sum of active 'Held' amounts.</summary>
    Task<LoyaltyBalanceView> GetBalanceAsync(string tenantId, string customerId, CancellationToken ct);

    /// <summary>Reserves <paramref name="amount"/> against the customer's balance — idempotent on retry for the same (tenant, order, customer).</summary>
    Task<LoyaltyReserveResult> ReserveAsync(string tenantId, string customerId, Guid orderId, decimal amount, TimeSpan ttl, CancellationToken ct);

    /// <summary>Captures the hold — writes a negative ledger row and marks the hold 'Captured'. Idempotent on already-captured.</summary>
    Task<LoyaltyCaptureResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct);

    /// <summary>Releases a still-held hold (rollback before capture). No ledger row written. Idempotent.</summary>
    Task<LoyaltyReleaseResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct);

    /// <summary>Refunds a captured hold — writes a positive ledger row that reverses the spend. Idempotent.</summary>
    Task<LoyaltyRefundResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct);

    /// <summary>Records an EARN/ADJUST ledger entry directly (no hold). Used by external earn flows.</summary>
    Task<long> RecordLedgerAsync(string tenantId, string customerId, decimal delta, string reason,
        Guid? orderId, string? notes, CancellationToken ct);
}

public sealed record LoyaltyBalanceView(string CustomerId, decimal Balance, decimal HeldAmount, decimal Available);
public sealed record LoyaltyReserveResult(bool Success, string? ErrorCode, string? ErrorMessage, LoyaltyHoldRow? Hold);
public sealed record LoyaltyCaptureResult(bool Success, string? ErrorCode, string? ErrorMessage, LoyaltyHoldRow? Hold);
public sealed record LoyaltyReleaseResult(bool Success, string? ErrorCode, string? ErrorMessage, LoyaltyHoldRow? Hold);
public sealed record LoyaltyRefundResult(bool Success, string? ErrorCode, string? ErrorMessage, LoyaltyHoldRow? Hold);
