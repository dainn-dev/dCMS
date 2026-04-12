using System.Text.RegularExpressions;

namespace dCMS.Web.InventoryProxy;

/// <summary>Restricts Inventory BFF paths to warehouse list + variant stock reads + stock mutations (DAI-292).</summary>
public static partial class InventoryProxyPathValidator
{
    [GeneratedRegex(
        @"^(?:warehouses|stock/(?:variants/[a-zA-Z0-9_-]{1,128}|adjust|reserve|release|bulk))$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPathRegex();

    public static bool IsAllowed(string pathSuffix)
    {
        if (string.IsNullOrWhiteSpace(pathSuffix))
            return false;
        var p = pathSuffix.Trim().TrimStart('/');
        if (p.Contains("..", StringComparison.Ordinal) || p.Contains("//", StringComparison.Ordinal))
            return false;
        var forMatch = p;
        var qi = p.IndexOf('?', StringComparison.Ordinal);
        if (qi >= 0)
            forMatch = p[..qi];
        return forMatch.Length <= 512 && AllowedPathRegex().IsMatch(forMatch);
    }
}
