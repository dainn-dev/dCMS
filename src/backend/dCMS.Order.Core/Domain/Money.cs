namespace dCMS.Order.Core.Domain;

/// <summary>Monetary amount in a single currency (ISO 4217 code).</summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public Money Add(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");

        return new Money(Amount + other.Amount, Currency);
    }

    public static Money Sum(IEnumerable<Money> values)
    {
        Money? acc = null;
        foreach (var m in values)
            acc = acc is null ? m : acc.Value.Add(m);
        return acc ?? new Money(0, "USD");
    }
}
