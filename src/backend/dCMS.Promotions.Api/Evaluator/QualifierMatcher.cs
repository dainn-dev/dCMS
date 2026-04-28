using System.Text.Json;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Matches cart lines against a campaign's QualifiersJson document.
/// Supported qualifier keys (any combination, all conditions ANDed):
///   - productIds: string[] — match line.ProductId
///   - variantIds: string[] — match line.VariantId
///   - categoryIds: string[] — match any of line.CategoryIds
///   - brandIds: string[] — match line.BrandId
///   - skus: string[] — match line.Sku (case-insensitive)
/// Empty/missing arrays are skipped (do not constrain). Returns all-match when QualifiersJson is empty/{}.
/// </summary>
public static class QualifierMatcher
{
    public static List<CartLine> MatchingLines(string qualifiersJson, IReadOnlyList<CartLine> lines)
    {
        var spec = ParseSpec(qualifiersJson);
        return lines.Where(l => spec.Matches(l)).ToList();
    }

    public static bool Matches(string qualifiersJson, CartLine line) =>
        ParseSpec(qualifiersJson).Matches(line);

    private static QualifierSpec ParseSpec(string qualifiersJson)
    {
        if (string.IsNullOrWhiteSpace(qualifiersJson)) return QualifierSpec.AllMatch;
        try
        {
            using var doc = JsonDocument.Parse(qualifiersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return QualifierSpec.AllMatch;
            return new QualifierSpec(
                ReadStringArray(doc.RootElement, "productIds"),
                ReadStringArray(doc.RootElement, "variantIds"),
                ReadStringArray(doc.RootElement, "categoryIds"),
                ReadStringArray(doc.RootElement, "brandIds"),
                ReadStringArray(doc.RootElement, "skus"));
        }
        catch (JsonException)
        {
            return QualifierSpec.NeverMatch;
        }
    }

    private static HashSet<string>? ReadStringArray(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in arr.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                set.Add(s);
        return set.Count == 0 ? null : set;
    }

    private sealed record QualifierSpec(
        HashSet<string>? ProductIds,
        HashSet<string>? VariantIds,
        HashSet<string>? CategoryIds,
        HashSet<string>? BrandIds,
        HashSet<string>? Skus)
    {
        public static readonly QualifierSpec AllMatch = new(null, null, null, null, null);
        public static readonly QualifierSpec NeverMatch = new(
            new HashSet<string>(), null, null, null, null);

        public bool Matches(CartLine line)
        {
            if (ProductIds is { Count: > 0 } && !ProductIds.Contains(line.ProductId)) return false;
            if (VariantIds is { Count: > 0 } && (line.VariantId is null || !VariantIds.Contains(line.VariantId))) return false;
            if (CategoryIds is { Count: > 0 } && !line.CategoryIds.Any(c => CategoryIds.Contains(c))) return false;
            if (BrandIds is { Count: > 0 } && (line.BrandId is null || !BrandIds.Contains(line.BrandId))) return false;
            if (Skus is { Count: > 0 } && !Skus.Contains(line.Sku)) return false;
            return true;
        }
    }
}
