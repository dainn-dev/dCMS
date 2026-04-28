using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-724: late-cancel compensation consumer. The OrderSaga publishes
/// <see cref="ReleasePaymentComponentsV1"/> when an order is cancelled after one or more
/// payment components have already captured. This consumer issues per-component refund
/// (for captured) or release (for authorised but uncaptured) calls, idempotent on
/// <c>(OrderId, ComponentId, REFUND|RELEASE)</c> via <see cref="IPaymentComponentDispatchLog"/>.
/// </summary>
public sealed class ReleasePaymentComponentsConsumer : IConsumer<ReleasePaymentComponentsV1>
{
    private readonly OrderPaymentRepository _payments;
    private readonly IPaymentComponentDispatchLog _log;
    private readonly IVoucherTenderClient _vouchers;
    private readonly ILoyaltyTenderClient _loyalty;
    private readonly ILogger<ReleasePaymentComponentsConsumer> _logger;

    public ReleasePaymentComponentsConsumer(
        OrderPaymentRepository payments,
        IPaymentComponentDispatchLog log,
        IVoucherTenderClient vouchers,
        ILoyaltyTenderClient loyalty,
        ILogger<ReleasePaymentComponentsConsumer> logger)
    {
        _payments = payments;
        _log = log;
        _vouchers = vouchers;
        _loyalty = loyalty;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleasePaymentComponentsV1> ctx)
    {
        var msg = ctx.Message;
        var payment = await _payments.GetByOrderIdAsync(msg.OrderId, ctx.CancellationToken);
        if (payment is null) return;

        foreach (var c in payment.Components)
        {
            if (!Guid.TryParse(c.ExternalRef, out var holdId) || holdId == Guid.Empty) continue;

            switch (c.State)
            {
                case PaymentComponentState.Captured:
                    await IssueRefundAsync(msg, c, holdId, ctx.CancellationToken);
                    break;
                case PaymentComponentState.Authorized:
                    await IssueReleaseAsync(msg, c, holdId, ctx.CancellationToken);
                    break;
                default:
                    // Pending/Failed/Refunded/Cancelled: nothing to do here.
                    break;
            }
        }

        await _payments.UpsertAsync(payment, ctx.CancellationToken);
    }

    private async Task IssueRefundAsync(ReleasePaymentComponentsV1 msg, PaymentComponent c, Guid holdId, CancellationToken ct)
    {
        var prior = await _log.TryGetAsync(msg.OrderId, c.Id, "REFUND", ct);
        if (prior is { IsSuccess: true })
        {
            c.Refund();
            return;
        }
        var result = c.Type switch
        {
            PaymentComponentType.Voucher => await _vouchers.RefundAsync(msg.TenantId, holdId, ct),
            PaymentComponentType.LoyaltyPoints => await _loyalty.RefundAsync(msg.TenantId, holdId, ct),
            _ => TenderCallResult.Ok(), // GiftCard/Gateway refund handled by Payment.Api via RefundPaymentV1
        };
        if (result.Success)
        {
            c.Refund();
            await _log.RecordSuccessAsync(msg.OrderId, c.Id, "REFUND", holdId.ToString(), ct);
        }
        else
        {
            await _log.RecordFailureAsync(msg.OrderId, c.Id, "REFUND", result.ErrorCode, result.ErrorMessage, ct);
            _logger.LogError("ReleasePaymentComponents: refund failed {Order}/{Component}: {Code} {Message}",
                msg.OrderId, c.Type, result.ErrorCode, result.ErrorMessage);
        }
    }

    private async Task IssueReleaseAsync(ReleasePaymentComponentsV1 msg, PaymentComponent c, Guid holdId, CancellationToken ct)
    {
        var prior = await _log.TryGetAsync(msg.OrderId, c.Id, "RELEASE", ct);
        if (prior is { IsSuccess: true })
        {
            c.Cancel();
            return;
        }
        var result = c.Type switch
        {
            PaymentComponentType.Voucher => await _vouchers.ReleaseAsync(msg.TenantId, holdId, msg.Reason, ct),
            PaymentComponentType.LoyaltyPoints => await _loyalty.ReleaseAsync(msg.TenantId, holdId, msg.Reason, ct),
            _ => TenderCallResult.Ok(),
        };
        if (result.Success)
        {
            c.Cancel();
            await _log.RecordSuccessAsync(msg.OrderId, c.Id, "RELEASE", holdId.ToString(), ct);
        }
        else
        {
            await _log.RecordFailureAsync(msg.OrderId, c.Id, "RELEASE", result.ErrorCode, result.ErrorMessage, ct);
            _logger.LogWarning("ReleasePaymentComponents: release failed {Order}/{Component}: {Code} {Message}",
                msg.OrderId, c.Type, result.ErrorCode, result.ErrorMessage);
        }
    }
}
