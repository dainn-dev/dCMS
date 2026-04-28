namespace dCMS.AspNetCore.Auth;

internal static class DcmsScopeClaimParser
{
    public static IReadOnlySet<string> ParseCsvClaim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<string>(StringComparer.Ordinal);

        // Keep it simple & allocation-light; IDs are expected to be short strings.
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        return new HashSet<string>(
            parts.Where(p => !string.IsNullOrWhiteSpace(p)),
            StringComparer.Ordinal);
    }
}

