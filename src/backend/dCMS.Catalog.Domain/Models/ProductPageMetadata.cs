namespace dCMS.Core.Models;

/// <summary>
/// Product Page / SEO metadata, storefront publish window and visibility/recommendation flags.
/// Multi-language values (page title, meta keywords, meta description) are stored as JSON maps
/// keyed by language code (e.g. <c>{"vi":"…","en":"…"}</c>).
/// </summary>
public sealed record ProductPageMetadata(
    string? PageTitleJson,
    string? MetaKeywordsJson,
    string? MetaDescriptionJson,
    DateTimeOffset? PublishFrom,
    DateTimeOffset? PublishUntil,
    bool RecommendSimilar = true,
    string? RecommendationsMode = "auto",
    bool RestockNotification = false);
