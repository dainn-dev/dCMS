namespace dCMS.Order.Core.Ordering;

/// <summary>Outcome of <see cref="IOrderService.CancelOrderAsync"/> (DAI-326).</summary>
public abstract record CancelOrderResult
{
    public sealed record Ok(Core.Domain.Order Order) : CancelOrderResult;

    public sealed record NotFound : CancelOrderResult;

    public sealed record Forbidden : CancelOrderResult;

    public sealed record NotCancellable(string Message) : CancelOrderResult;

    /// <summary>Order was already <see cref="Core.Domain.OrderStatus.Cancelled"/> (idempotent replay).</summary>
    public sealed record AlreadyCancelled(Core.Domain.Order Order) : CancelOrderResult;
}
