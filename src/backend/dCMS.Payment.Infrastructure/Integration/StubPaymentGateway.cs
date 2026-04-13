using System.Collections.Concurrent;
using dCMS.Payment.Core;

namespace dCMS.Payment.Infrastructure.Integration;

/// <summary>Dev stub — returns deterministic intent id + checkout URL (DAI-339); process is idempotent per intent id (DAI-340).</summary>
public sealed class StubPaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, byte> _chargedIntents = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _refundsByIntent = new(StringComparer.Ordinal);

    public Task<PaymentGatewayIntent> CreateIntentAsync(
        CreatePaymentIntentGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        var orderKey = request.OrderId.ToString("D");
        var intentId = $"pi_stub_{orderKey}";
        var url = $"https://checkout.local/pay/{Uri.EscapeDataString(orderKey)}";
        return Task.FromResult(new PaymentGatewayIntent(intentId, url));
    }

    public Task<ProcessPaymentGatewayResult> ProcessPaymentAsync(
        ProcessPaymentGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
            return Task.FromResult<ProcessPaymentGatewayResult>(
                new ProcessPaymentGatewayResult.Failed("invalid_intent"));

        if (request.PaymentIntentId.Contains("decline", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<ProcessPaymentGatewayResult>(
                new ProcessPaymentGatewayResult.Failed("card_declined"));

        var chargeId = $"ch_stub_{request.PaymentIntentId}";
        if (!_chargedIntents.TryAdd(request.PaymentIntentId, 0))
            return Task.FromResult<ProcessPaymentGatewayResult>(
                new ProcessPaymentGatewayResult.AlreadySucceeded(chargeId));

        return Task.FromResult<ProcessPaymentGatewayResult>(
            new ProcessPaymentGatewayResult.Succeeded(chargeId));
    }

    public Task<RefundPaymentGatewayResult> RefundPaymentAsync(
        RefundPaymentGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
            return Task.FromResult<RefundPaymentGatewayResult>(
                new RefundPaymentGatewayResult.Failed("invalid_intent"));

        if (_refundsByIntent.TryGetValue(request.PaymentIntentId, out var existing))
            return Task.FromResult<RefundPaymentGatewayResult>(
                new RefundPaymentGatewayResult.AlreadyRefunded(existing));

        var refundId = $"re_stub_{request.PaymentIntentId}";
        _refundsByIntent[request.PaymentIntentId] = refundId;
        return Task.FromResult<RefundPaymentGatewayResult>(
            new RefundPaymentGatewayResult.Succeeded(refundId));
    }
}
