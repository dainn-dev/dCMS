using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Per-request mutable state passed to mechanic strategies.
/// Tracks accumulated discount per line so subsequent mechanics see remaining capacity.
/// </summary>
public sealed class EvaluationContext
{
    public EvaluateRequest Request { get; }
    public Dictionary<string, decimal> AccumulatedLineDiscount { get; } = new();
    public List<LineAdjustment> LineAdjustments { get; } = new();
    public List<OrderAdjustment> OrderAdjustments { get; } = new();
    public List<AppliedPromotion> AppliedPromotions { get; } = new();
    public List<Suggestion> Suggestions { get; } = new();
    public List<IssuedCodePromise> IssuedCodes { get; } = new();

    public EvaluationContext(EvaluateRequest request)
    {
        Request = request;
        foreach (var line in request.Lines)
            AccumulatedLineDiscount[line.LineId] = 0m;
    }

    /// <summary>Remaining headroom on a line: total - already-discounted. Never negative.</summary>
    public decimal RemainingOnLine(CartLine line)
    {
        var lineTotal = line.UnitPrice * line.Quantity;
        var used = AccumulatedLineDiscount.GetValueOrDefault(line.LineId, 0m);
        return Math.Max(0m, lineTotal - used);
    }

    public void AddLineDiscount(string lineId, string campaignId, decimal amount, string reason)
    {
        if (amount <= 0m) return;
        AccumulatedLineDiscount[lineId] = AccumulatedLineDiscount.GetValueOrDefault(lineId, 0m) + amount;
        LineAdjustments.Add(new LineAdjustment(lineId, campaignId, amount, reason));
    }

    public void AddOrderDiscount(string campaignId, decimal amount, string reason)
    {
        if (amount <= 0m) return;
        OrderAdjustments.Add(new OrderAdjustment(campaignId, amount, reason));
    }
}
