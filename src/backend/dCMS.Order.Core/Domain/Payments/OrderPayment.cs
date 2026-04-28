namespace dCMS.Order.Core.Domain.Payments;

/// <summary>
/// DAI-722: Aggregate that owns the multi-tender payment plan for one order. Sum of component
/// Amounts must equal Total (AC2). Status transitions are derived from component states.
/// </summary>
public sealed class OrderPayment
{
    private readonly List<PaymentComponent> _components;

    public Guid Id { get; }
    public Guid OrderId { get; }
    public decimal Total { get; private set; }
    public string Status { get; private set; }
    public IReadOnlyList<PaymentComponent> Components => _components;

    public OrderPayment(Guid id, Guid orderId, decimal total, string status, IEnumerable<PaymentComponent>? components = null)
    {
        if (total < 0m) throw new ArgumentOutOfRangeException(nameof(total));
        Id = id;
        OrderId = orderId;
        Total = total;
        Status = status;
        _components = components?.OrderBy(c => c.Ordering).ToList() ?? new();
        EnsureSumInvariant();
    }

    public static OrderPayment Plan(
        Guid orderId,
        decimal total,
        IEnumerable<(PaymentComponentType Type, decimal Amount, string? Reference)> tenders)
    {
        var ordered = tenders
            .Where(t => t.Amount > 0m)
            .OrderBy(t => OrderingFor(t.Type))
            .Select((t, i) => new PaymentComponent(Guid.NewGuid(), t.Type, t.Amount, i, reference: t.Reference))
            .ToList();
        return new OrderPayment(Guid.NewGuid(), orderId, total, "Pending", ordered);
    }

    /// <summary>Backwards-compat overload — used by existing tests that don't care about Reference.</summary>
    public static OrderPayment Plan(
        Guid orderId,
        decimal total,
        IEnumerable<(PaymentComponentType Type, decimal Amount)> tenders)
        => Plan(orderId, total, tenders.Select(t => (t.Type, t.Amount, (string?)null)));

    /// <summary>Vouchers consume first, then LoyaltyPoints, then any remainder via Gateway/GiftCard.</summary>
    private static int OrderingFor(PaymentComponentType type) => type switch
    {
        PaymentComponentType.Voucher => 0,
        PaymentComponentType.LoyaltyPoints => 1,
        PaymentComponentType.GiftCard => 2,
        PaymentComponentType.Gateway => 3,
        _ => 99,
    };

    public void RecomputeStatus()
    {
        if (_components.Count == 0)
        {
            Status = "Pending";
            return;
        }
        if (_components.All(c => c.State == PaymentComponentState.Captured))
            Status = "Captured";
        else if (_components.Any(c => c.State == PaymentComponentState.Failed))
            Status = "Failed";
        else if (_components.All(c => c.State is PaymentComponentState.Authorized or PaymentComponentState.Captured))
            Status = "Authorized";
        else if (_components.All(c => c.State == PaymentComponentState.Refunded))
            Status = "Refunded";
        else if (_components.All(c => c.State == PaymentComponentState.Cancelled))
            Status = "Cancelled";
        else
            Status = "Pending";
    }

    /// <summary>AC2: sum of component amounts must equal Total.</summary>
    public void EnsureSumInvariant()
    {
        if (_components.Count == 0) return;
        var sum = _components.Sum(c => c.Amount);
        if (Math.Abs(sum - Total) > 0.0001m)
            throw new InvalidOperationException(
                $"Payment components sum {sum} does not equal order total {Total}.");
    }
}
