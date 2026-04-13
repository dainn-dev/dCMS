namespace dCMS.Order.Core.Ordering;

/// <summary>Outcome of <see cref="IOrderService.CreateOrderAsync"/> including redirect URL from Payment (DAI-315).</summary>
public sealed record CreateOrderResult(
    global::dCMS.Order.Core.Domain.Order Order,
    string? PaymentUrl,
    bool IsIdempotentReplay = false);
