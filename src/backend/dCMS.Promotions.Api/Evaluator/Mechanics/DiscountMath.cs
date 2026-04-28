using System.Text.Json;

namespace dCMS.Promotions.Api.Evaluator.Mechanics;

internal static class DiscountMath
{
    /// <summary>
    /// Compute discount amount on a base price given discount type ("percent" | "fixed") and value.
    /// Result is rounded to 2 decimals (banker's rounding) and capped at <paramref name="basePrice"/>.
    /// </summary>
    public static decimal Compute(string discountType, decimal discountValue, decimal basePrice)
    {
        if (basePrice <= 0m || discountValue <= 0m) return 0m;
        var raw = discountType.ToLowerInvariant() switch
        {
            "percent" or "percentage" => basePrice * (discountValue / 100m),
            "fixed" or "amount"       => discountValue,
            _ => 0m,
        };
        if (raw < 0m) raw = 0m;
        if (raw > basePrice) raw = basePrice;
        return Math.Round(raw, 2, MidpointRounding.ToEven);
    }

    public static string ReadString(JsonElement obj, string name, string fallback = "")
    {
        if (obj.ValueKind != JsonValueKind.Object) return fallback;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return fallback;
        return el.GetString() ?? fallback;
    }

    public static decimal ReadDecimal(JsonElement obj, string name, decimal fallback = 0m)
    {
        if (obj.ValueKind != JsonValueKind.Object) return fallback;
        if (!obj.TryGetProperty(name, out var el)) return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(el.GetString(), out var d) => d,
            _ => fallback,
        };
    }

    public static int ReadInt(JsonElement obj, string name, int fallback = 0)
    {
        if (obj.ValueKind != JsonValueKind.Object) return fallback;
        if (!obj.TryGetProperty(name, out var el)) return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(el.GetString(), out var i) => i,
            _ => fallback,
        };
    }
}
