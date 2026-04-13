namespace dCMS.Order.Core.Ordering;

/// <summary>Outcome of <see cref="IOrderService.CreateOrderAsync"/>. <see cref="PaymentUrl"/> is null until Payment Service responds via saga (US-19).</summary>
public sealed record CreateOrderResult(
    global::dCMS.Order.Core.Domain.Order Order,
    string? PaymentUrl,
    bool IsIdempotentReplay = false);
