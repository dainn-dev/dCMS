using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-724: Multi-tender payment orchestrator. Consumes <see cref="ProcessPaymentV1"/> from the
/// OrderSaga, walks the components on the <see cref="OrderPayment"/> in canonical order
/// (Voucher → LoyaltyPoints → GiftCard → Gateway), Reserves+Captures balance-bound tenders via
/// <see cref="IVoucherTenderClient"/> / <see cref="ILoyaltyTenderClient"/>, and publishes the
/// terminal <see cref="PaymentCompletedV1"/> or <see cref="PaymentFailedV1"/> back to the saga.
///
/// Idempotency is keyed via <see cref="IPaymentComponentDispatchLog"/> on
/// <c>(OrderId, ComponentId, Action)</c>; on retry, prior outcomes are replayed without re-calling
/// the downstream API.
///
/// Inline compensation: any reserve/capture failure releases all prior holds before the saga is
/// notified of <see cref="PaymentFailedV1"/>. Late cancels (after capture) are handled separately
/// by <see cref="ReleasePaymentComponentsConsumer"/> which consumes <see cref="ReleasePaymentComponentsV1"/>.
/// </summary>
public sealed class PaymentOrchestrator : IConsumer<ProcessPaymentV1>
{
    private readonly OrderPaymentRepository _payments;
    private readonly IPaymentComponentDispatchLog _log;
    private readonly IVoucherTenderClient _vouchers;
    private readonly ILoyaltyTenderClient _loyalty;
    private readonly ILogger<PaymentOrchestrator> _logger;

    public PaymentOrchestrator(
        OrderPaymentRepository payments,
        IPaymentComponentDispatchLog log,
        IVoucherTenderClient vouchers,
        ILoyaltyTenderClient loyalty,
        ILogger<PaymentOrchestrator> logger)
    {
        _payments = payments;
        _log = log;
        _vouchers = vouchers;
        _loyalty = loyalty;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessPaymentV1> ctx)
    {
        var msg = ctx.Message;
        if (!Guid.TryParse(msg.OrderId, out var orderGuid))
        {
            _logger.LogWarning("PaymentOrchestrator: invalid OrderId {OrderId}", msg.OrderId);
            return;
        }

        var payment = await _payments.GetByOrderIdAsync(orderGuid, ctx.CancellationToken);
        if (payment is null)
        {
            // No multi-tender plan persisted → fall through to the existing single-tender path.
            // The legacy ProcessPaymentConsumer will pick this up; nothing to do here.
            return;
        }

        var capturedSoFar = new List<PaymentComponent>();

        foreach (var component in payment.Components.OrderBy(c => c.Ordering))
        {
            if (component.State is PaymentComponentState.Captured)
            {
                capturedSoFar.Add(component);
                continue;
            }

            var reserve = await DispatchReserveAsync(orderGuid, msg, component, ctx.CancellationToken);
            if (!reserve.Success)
            {
                component.Fail(reserve.ErrorMessage ?? reserve.ErrorCode ?? "reserve_failed");
                await CompensateAsync(orderGuid, msg.TenantId, capturedSoFar, $"reserve_failed:{component.Type}", ctx.CancellationToken);
                await _payments.UpsertAsync(payment, ctx.CancellationToken);
                await PublishFailedAsync(ctx, msg, component.Type.ToString(), reserve);
                return;
            }
            component.Authorize(reserve.ExternalRef ?? string.Empty);

            var capture = await DispatchCaptureAsync(orderGuid, msg.TenantId, component, ctx.CancellationToken);
            if (!capture.Success)
            {
                component.Fail(capture.ErrorMessage ?? capture.ErrorCode ?? "capture_failed");
                // Release the failing reserve plus all prior captured components.
                await ReleaseSingleAsync(orderGuid, msg.TenantId, component, "capture_failed", ctx.CancellationToken);
                await CompensateAsync(orderGuid, msg.TenantId, capturedSoFar, $"capture_failed:{component.Type}", ctx.CancellationToken);
                await _payments.UpsertAsync(payment, ctx.CancellationToken);
                await PublishFailedAsync(ctx, msg, component.Type.ToString(), capture);
                return;
            }
            component.Capture();
            capturedSoFar.Add(component);
        }

        await _payments.UpsertAsync(payment, ctx.CancellationToken);
        await ctx.Publish(new PaymentCompletedV1(
            msg.OrderId, payment.Id.ToString(), msg.TenantId, StoreId: string.Empty, DateTimeOffset.UtcNow));
    }

    private async Task<TenderCallResult> DispatchReserveAsync(Guid orderId, ProcessPaymentV1 msg, PaymentComponent component, CancellationToken ct)
    {
        var prior = await _log.TryGetAsync(orderId, component.Id, "RESERVE", ct);
        if (prior is { IsSuccess: true })
            return TenderCallResult.Ok(prior.ExternalRef);
        if (prior is { IsSuccess: false })
            return TenderCallResult.Fail(prior.ErrorCode ?? "reserve_failed", prior.ErrorMessage ?? "previous reserve failed");

        TenderCallResult result = component.Type switch
        {
            PaymentComponentType.Voucher
                // Voucher reserve is keyed by code; we don't have it on the component yet, so fall back to OrderId.
                // In practice, the placement flow records the voucher code in component.ExternalRef *before* publishing
                // ProcessPaymentV1 (or in a future story, on a dedicated VoucherCode column).
                => await _vouchers.ReserveAsync(msg.TenantId, code: component.ExternalRef ?? msg.OrderId, orderId, component.Amount, ct),
            PaymentComponentType.LoyaltyPoints
                => await _loyalty.ReserveAsync(msg.TenantId, msg.CustomerId, orderId, component.Amount, ct),
            // GiftCard / Gateway components flow through the existing Payment.Api path: treat reserve as a no-op success.
            _ => TenderCallResult.Ok(null),
        };

        if (result.Success)
            await _log.RecordSuccessAsync(orderId, component.Id, "RESERVE", result.ExternalRef, ct);
        else
            await _log.RecordFailureAsync(orderId, component.Id, "RESERVE", result.ErrorCode, result.ErrorMessage, ct);
        return result;
    }

    private async Task<TenderCallResult> DispatchCaptureAsync(Guid orderId, string tenantId, PaymentComponent component, CancellationToken ct)
    {
        if (component.Type is PaymentComponentType.GiftCard or PaymentComponentType.Gateway)
            return TenderCallResult.Ok(); // gateway capture handled by Payment.Api

        if (!Guid.TryParse(component.ExternalRef, out var holdId) || holdId == Guid.Empty)
            return TenderCallResult.Fail("missing_hold", "no hold id from reserve to capture");

        var prior = await _log.TryGetAsync(orderId, component.Id, "CAPTURE", ct);
        if (prior is { IsSuccess: true }) return TenderCallResult.Ok(prior.ExternalRef);
        if (prior is { IsSuccess: false })
            return TenderCallResult.Fail(prior.ErrorCode ?? "capture_failed", prior.ErrorMessage ?? "previous capture failed");

        var result = component.Type switch
        {
            PaymentComponentType.Voucher => await _vouchers.CaptureAsync(tenantId, holdId, ct),
            PaymentComponentType.LoyaltyPoints => await _loyalty.CaptureAsync(tenantId, holdId, ct),
            _ => TenderCallResult.Ok(),
        };

        if (result.Success)
            await _log.RecordSuccessAsync(orderId, component.Id, "CAPTURE", null, ct);
        else
            await _log.RecordFailureAsync(orderId, component.Id, "CAPTURE", result.ErrorCode, result.ErrorMessage, ct);
        return result;
    }

    private async Task ReleaseSingleAsync(Guid orderId, string tenantId, PaymentComponent component, string reason, CancellationToken ct)
    {
        if (!Guid.TryParse(component.ExternalRef, out var holdId) || holdId == Guid.Empty) return;
        var result = component.Type switch
        {
            PaymentComponentType.Voucher => await _vouchers.ReleaseAsync(tenantId, holdId, reason, ct),
            PaymentComponentType.LoyaltyPoints => await _loyalty.ReleaseAsync(tenantId, holdId, reason, ct),
            _ => TenderCallResult.Ok(),
        };
        if (!result.Success)
            _logger.LogWarning("PaymentOrchestrator: release failed for {Component}/{Hold}: {Code} {Message}",
                component.Type, holdId, result.ErrorCode, result.ErrorMessage);
        else
            await _log.RecordSuccessAsync(orderId, component.Id, "RELEASE", holdId.ToString(), ct);
    }

    private async Task CompensateAsync(Guid orderId, string tenantId, IEnumerable<PaymentComponent> captured, string reason, CancellationToken ct)
    {
        // Reverse order: refund captured components in opposite order to capture.
        foreach (var c in captured.Reverse())
        {
            if (!Guid.TryParse(c.ExternalRef, out var holdId) || holdId == Guid.Empty) continue;
            var result = c.Type switch
            {
                PaymentComponentType.Voucher => await _vouchers.RefundAsync(tenantId, holdId, ct),
                PaymentComponentType.LoyaltyPoints => await _loyalty.RefundAsync(tenantId, holdId, ct),
                _ => TenderCallResult.Ok(),
            };
            if (result.Success)
            {
                c.Refund();
                await _log.RecordSuccessAsync(orderId, c.Id, "REFUND", holdId.ToString(), ct);
            }
            else
            {
                _logger.LogError("PaymentOrchestrator: compensation refund failed for {Component}/{Hold} reason={Reason}: {Code} {Message}",
                    c.Type, holdId, reason, result.ErrorCode, result.ErrorMessage);
            }
        }
    }

    private static Task PublishFailedAsync(ConsumeContext ctx, ProcessPaymentV1 msg, string failedComponent, TenderCallResult result)
        => ctx.Publish(new PaymentFailedV1(
            msg.OrderId,
            $"{failedComponent}:{result.ErrorCode}:{result.ErrorMessage}",
            msg.TenantId,
            StoreId: string.Empty,
            DateTimeOffset.UtcNow));
}
