using System.Text.RegularExpressions;

namespace dCMS.Web.CatalogProxy;

/// <summary>Restricts forwarded paths to tenant product APIs only (no arbitrary Catalog surface).</summary>
public static partial class CatalogProxyPathValidator
{
    /// <summary>Relative path under <c>/api/v1/tenants/{t}/stores/{s}/</c> — only <c>products…</c> branches.</summary>
    /// <summary>
    /// Allows <c>products/{id}/variants</c>, <c>…/variants/generate</c>, <c>…/variants/{variantId}</c>, lifecycle
    /// <c>publish</c> / <c>submit-for-approval</c> / <c>hide</c> / <c>unhide</c> / <c>archive</c> / <c>approval-comments</c> / <c>approve</c> / <c>request-changes</c> / <c>reject</c> (US-13, DAI-295, DAI-296).
    /// </summary>
    [GeneratedRegex(
        @"^products(?:/[a-zA-Z0-9_-]{1,128}(?:/(?:variants(?:/(?:generate|[a-zA-Z0-9_-]{1,128}))?|images(?:/(?:checksum-check|order|presign|[a-zA-Z0-9_-]{1,128}(?:/(?:content|s3-complete))?))?|publish|submit-for-approval|hide|unhide|archive|approval-comments|approve|request-changes|reject))?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPathRegex();

    public static bool IsAllowed(string pathSuffix)
    {
        if (string.IsNullOrWhiteSpace(pathSuffix))
            return false;
        var p = pathSuffix.Trim().TrimStart('/');
        if (p.Contains("..", StringComparison.Ordinal) || p.Contains("//", StringComparison.Ordinal))
            return false;
        if (p.Equals("categories", StringComparison.Ordinal))
            return true;
        if (p.Equals("variant-axes", StringComparison.Ordinal))
            return true;
        if (p.Equals("store-catalog-settings", StringComparison.Ordinal))
            return true;
        var forMatch = p;
        var qi = p.IndexOf('?', StringComparison.Ordinal);
        if (qi >= 0)
            forMatch = p[..qi];
        return forMatch.Length <= 512 && AllowedPathRegex().IsMatch(forMatch);
    }
}
