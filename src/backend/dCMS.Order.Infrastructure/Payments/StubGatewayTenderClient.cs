using System.Collections.Concurrent;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-689: dev/test stub for the multi-tender Gateway component. Mirrors the existing
/// <c>StubPaymentGateway</c> in <c>dCMS.Payment.Infrastructure</c> but conforms to
/// <see cref="IGatewayTenderClient"/>. Failure scenarios:
///  - customerId contains "decline" → card_declined
///  - customerId contains "timeout" → throws OperationCanceledException
///  - amount cents end in 99 → insufficient_funds
/// Authorize/capture/refund are idempotent on chargeRef.
/// </summary>
public sealed class StubGatewayTenderClient : IGatewayTenderClient
{
    private readonly ConcurrentDictionary<string, byte> _captured = new();
    private readonly ConcurrentDictionary<string, byte> _refunded = new();
    private readonly ConcurrentDictionary<string, byte> _voided = new();

    public Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct)
    {
        if (customerId?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
            throw new OperationCanceledException("stub gateway timeout");

        if (customerId?.Contains("decline", StringComparison.OrdinalIgnoreCase) == true)
            return Task.FromResult(TenderCallResult.Fail("card_declined", "Stub: customer flagged decline."));

        var cents = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        if (cents % 100m == 99m)
            return Task.FromResult(TenderCallResult.Fail("insufficient_funds", "Stub: amount ends in .99."));

        return Task.FromResult(TenderCallResult.Ok($"ch_stub_{orderId:N}"));
    }

    public Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_charge_ref", "chargeRef required."));
        if (_voided.ContainsKey(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_state", "Authorization was voided."));
        _captured.TryAdd(chargeRef, 0);
        return Task.FromResult(TenderCallResult.Ok(chargeRef));
    }

    public Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_charge_ref", "chargeRef required."));
        if (_captured.ContainsKey(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_state", "Already captured; use Refund."));
        _voided.TryAdd(chargeRef, 0);
        return Task.FromResult(TenderCallResult.Ok(chargeRef));
    }

    public Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_charge_ref", "chargeRef required."));
        if (!_captured.ContainsKey(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_state", "Cannot refund uncaptured charge."));
        _refunded.TryAdd(chargeRef, 0);
        return Task.FromResult(TenderCallResult.Ok(chargeRef));
    }
}
