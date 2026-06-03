namespace dCMS.Core.Exceptions;

/// <summary>Cart or per-user quantity exceeds configured store limits.</summary>
public sealed class QuantityLimitExceededException : Exception
{
    public QuantityLimitExceededException(
        string productId,
        int requested,
        int limit,
        string limitScope,
        string? ruleId = null,
        string? ruleName = null)
        : base(BuildMessage(productId, requested, limit, limitScope, ruleName))
    {
        ProductId = productId;
        Requested = requested;
        Limit = limit;
        LimitScope = limitScope;
        RuleId = ruleId;
        RuleName = ruleName;
    }

    public string ProductId { get; }
    public int Requested { get; }
    public int Limit { get; }
    /// <summary><c>per_cart</c> or <c>per_user</c>.</summary>
    public string LimitScope { get; }
    public string? RuleId { get; }
    public string? RuleName { get; }

    private static string BuildMessage(string productId, int requested, int limit, string scope, string? ruleName)
    {
        var rule = string.IsNullOrWhiteSpace(ruleName) ? "" : $" (rule: {ruleName})";
        return scope switch
        {
            "per_user" => $"Quantity limit exceeded for product {productId}{rule}: requested {requested}, limit {limit} per user.",
            _ => $"Quantity limit exceeded for product {productId}{rule}: requested {requested}, limit {limit} per cart."
        };
    }
}
