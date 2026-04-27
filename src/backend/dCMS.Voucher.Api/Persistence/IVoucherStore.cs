using dCMS.Voucher.Api.Domain;

namespace dCMS.Voucher.Api.Persistence;

public interface IVoucherStore
{
    Task<VoucherRow?> GetByCodeAsync(string tenantId, string code, CancellationToken ct);

    /// <summary>Reserves <paramref name="amount"/> against the voucher; idempotent on retry for the same (tenant, order, code).</summary>
    Task<ReserveResult> ReserveAsync(string tenantId, string code, Guid orderId, decimal amount, TimeSpan ttl, CancellationToken ct);

    /// <summary>Captures (commits) an existing 'Held' hold. Idempotent — a 'Captured' hold returns Ok with the same amount.</summary>
    Task<CaptureResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct);

    /// <summary>Releases a still-held hold (rollback before capture). Idempotent — already-released returns Ok.</summary>
    Task<ReleaseResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct);

    /// <summary>Refunds a captured hold — restores 'RemainingValue' and writes a REFUND ledger entry.</summary>
    Task<RefundResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct);

    Task<BalanceView?> GetBalanceAsync(string tenantId, string code, CancellationToken ct);
}

public sealed record ReserveResult(bool Success, string? ErrorCode, string? ErrorMessage, VoucherHoldRow? Hold);
public sealed record CaptureResult(bool Success, string? ErrorCode, string? ErrorMessage, VoucherHoldRow? Hold);
public sealed record ReleaseResult(bool Success, string? ErrorCode, string? ErrorMessage, VoucherHoldRow? Hold);
public sealed record RefundResult(bool Success, string? ErrorCode, string? ErrorMessage, VoucherHoldRow? Hold);
public sealed record BalanceView(string Code, decimal FaceValue, decimal RemainingValue, decimal HeldValue, string Currency, string Status);
