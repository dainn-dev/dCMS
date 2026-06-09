namespace dCMS.Order.Core.Ordering;

/// <summary>Outcome of <see cref="IOrderService.CreateOrderAsync"/>. <see cref="PaymentUrl"/> is set from sync create-intent (US-18/DAI-315).</summary>
public sealed record CreateOrderResult(
    global::dCMS.Order.Core.Domain.Order Order,
    string? PaymentUrl,
    bool IsIdempotentReplay = false);
