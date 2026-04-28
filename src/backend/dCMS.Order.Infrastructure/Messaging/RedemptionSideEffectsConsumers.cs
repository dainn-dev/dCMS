using dCMS.Core.Messaging;
using dCMS.Order.Core.Integration;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Promotions.Contracts.Evaluate;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Messaging;

/// <summary>
/// DAI-693 — When the saga reaches <c>Confirmed</c>, call Promotions to confirm the redemption.
/// Idempotent: Promotions.Api uses UNIQUE (tenant, promoCode, order) so retries are safe.
/// No-op when <c>OrderPromotions</c> is empty (snapshot persistence not yet wired in the create path).
/// </summary>
public sealed class OrderRedemptionConfirmConsumer : IConsumer<OrderPaymentSettledV1>
{
    private readonly OrderPromotionSnapshotReader _snapshots;
    private readonly IPromotionsClient _promotions;
    private readonly ILogger<OrderRedemptionConfirmConsumer> _logger;

    public OrderRedemptionConfirmConsumer(
        OrderPromotionSnapshotReader snapshots,
        IPromotionsClient promotions,
        ILogger<OrderRedemptionConfirmConsumer> logger)
    {
        _snapshots = snapshots;
        _promotions = promotions;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPaymentSettledV1> context)
    {
        var m = context.Message;
        var promoCodeId = await _snapshots
            .GetPromoCodeIdAsync(m.TenantId, m.OrderId, context.CancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(promoCodeId))
            return;

        var rows = await _snapshots
            .GetByOrderAsync(m.TenantId, m.OrderId, context.CancellationToken)
            .ConfigureAwait(false);

        var amount = rows.Sum(r => r.Amount);
        if (amount <= 0m)
            return;

        try
        {
            await _promotions.ConfirmRedemptionAsync(
                m.TenantId,
                new ConfirmRedemptionRequest(m.OrderId, promoCodeId!, null, amount, "VND"),
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Promotions confirm failed for order {OrderId} tenant {TenantId} — will rely on saga retry/outbox replay",
                m.OrderId, m.TenantId);
            throw;
        }
    }
}

/// <summary>
/// DAI-693 — When the saga reaches <c>Cancelled</c>, release the redemption so the cap is freed.
/// </summary>
public sealed class OrderRedemptionReleaseConsumer : IConsumer<OrderCancelledV1>
{
    private readonly OrderPromotionSnapshotReader _snapshots;
    private readonly IPromotionsClient _promotions;
    private readonly ILogger<OrderRedemptionReleaseConsumer> _logger;

    public OrderRedemptionReleaseConsumer(
        OrderPromotionSnapshotReader snapshots,
        IPromotionsClient promotions,
        ILogger<OrderRedemptionReleaseConsumer> logger)
    {
        _snapshots = snapshots;
        _promotions = promotions;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCancelledV1> context)
    {
        var m = context.Message;
        var promoCodeId = await _snapshots
            .GetPromoCodeIdAsync(m.TenantId, m.OrderId, context.CancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(promoCodeId))
            return;

        try
        {
            await _promotions.ReleaseRedemptionAsync(
                m.TenantId,
                new ReleaseRedemptionRequest(m.OrderId),
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Promotions release failed for order {OrderId} tenant {TenantId} — will rely on saga retry",
                m.OrderId, m.TenantId);
            throw;
        }
    }
}
