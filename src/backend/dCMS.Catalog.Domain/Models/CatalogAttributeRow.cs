namespace dCMS.Core.Models;

/// <summary>
/// Tenant-scoped product attribute definition (DAI-592).
/// Maps to extended CatalogAttributes table (migration 019).
/// </summary>
public sealed record CatalogAttributeRow(
    int            Id,
    string         TenantId,
    string         Name,
    string         Code,
    string         Type,
    bool           Required,
    string         Description,
    int            SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Valid attribute types matching SPA + DB contract.</summary>
    public static readonly IReadOnlySet<string> ValidTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "TEXT", "COLOR", "IMAGE", "SELECT", "BOOLEAN" };

    /// <summary>Code must be snake_case: starts with letter, followed by letters/digits/underscores.</summary>
    public static bool IsValidCode(string code) =>
        !string.IsNullOrWhiteSpace(code) &&
        code.Length <= 100 &&
        System.Text.RegularExpressions.Regex.IsMatch(code, @"^[a-z][a-z0-9_]*$");
}

/// <summary>
/// A single value belonging to a <see cref="CatalogAttributeRow"/> (DAI-592).
/// Maps to extended CatalogAttributeValues table (migration 019).
/// </summary>
public sealed record CatalogAttributeValueRow(
    int    Id,
    int    AttributeId,
    string Name,
    string Code,
    string ColorHex,
    string ImageUrl,
    int    SortOrder,
    DateTimeOffset CreatedAt);
