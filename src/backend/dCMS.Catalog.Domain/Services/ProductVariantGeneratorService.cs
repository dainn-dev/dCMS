using System.Security.Cryptography;
using System.Text;

namespace dCMS.Core.Services;

/// <summary>Cartesian variant generation + canonical combination hash (spec Section 2).</summary>
public static class ProductVariantGeneratorService
{
    public sealed record VariantAxisDefinition(int AttributeId, IReadOnlyList<int> ValueIds);

    /// <summary>Cartesian product of attribute value axes. Empty if any axis has zero values or axes list empty.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<int, int>> GenerateCombinations(IReadOnlyList<VariantAxisDefinition> axes)
    {
        if (axes.Count == 0)
            return Array.Empty<IReadOnlyDictionary<int, int>>();

        if (axes.Any(a => a.ValueIds.Count == 0))
            return Array.Empty<IReadOnlyDictionary<int, int>>();

        var ids = axes.Select(a => a.AttributeId).ToArray();
        if (ids.Length != ids.Distinct().Count())
            throw new ArgumentException("Duplicate attribute axis.", nameof(axes));

        return Cartesian(axes).Select(d => (IReadOnlyDictionary<int, int>)new Dictionary<int, int>(d)).ToList();
    }

    /// <summary>Canonical: sort by AttributeId ascending, join <c>id=value</c> with <c>|</c>, UTF-8 SHA-256 → lowercase hex.</summary>
    public static string ComputeCombinationHash(IReadOnlyDictionary<int, int> assignment)
    {
        if (assignment.Count == 0)
            throw new ArgumentException("Assignment cannot be empty.", nameof(assignment));

        var canonical = string.Join("|",
            assignment.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IEnumerable<Dictionary<int, int>> Cartesian(IReadOnlyList<VariantAxisDefinition> axes) =>
        Loop(axes, 0, new Dictionary<int, int>());

    private static IEnumerable<Dictionary<int, int>> Loop(IReadOnlyList<VariantAxisDefinition> axes, int depth,
        Dictionary<int, int> current)
    {
        if (depth == axes.Count)
        {
            yield return new Dictionary<int, int>(current);
            yield break;
        }

        var axis = axes[depth];
        foreach (var valueId in axis.ValueIds)
        {
            var next = new Dictionary<int, int>(current) { [axis.AttributeId] = valueId };
            foreach (var row in Loop(axes, depth + 1, next))
                yield return row;
        }
    }
}
