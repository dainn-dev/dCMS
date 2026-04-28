namespace dCMS.Core.Models;

/// <summary>
/// Full category row for backoffice management (DAI-586).
/// Includes all fields added by migration 018_ExtendCategories.
/// </summary>
public sealed record CatalogCategoryRow(
    int    Id,
    string TenantId,
    int?   ParentId,
    string Path,
    int    Depth,
    string Name,
    string Slug,
    int    SortOrder,

    // ── Status / schedule ────────────────────────────────────────────────────
    bool             Active,
    DateTimeOffset?  PublishFrom,
    DateTimeOffset?  PublishUntil,

    // ── Images ───────────────────────────────────────────────────────────────
    string ImageMenuUrl,
    string ImagePageUrl,
    string ImageThumbUrl,

    // ── Navbar settings ──────────────────────────────────────────────────────
    bool   ShowInNav,
    bool   ShowInBrands,
    string CustomNavUrl,
    int    NavSortPriority,
    bool   BreakNavColumn,

    // ── Product page settings ─────────────────────────────────────────────────
    string DefaultSort,
    bool   NoRecommendations,

    // ── SEO (multi-language JSON) ─────────────────────────────────────────────
    string MetaTitleJson,
    string MetaKeywordsJson,
    string MetaDescJson,

    // ── Access control ────────────────────────────────────────────────────────
    bool   RestrictAccess,
    string AccessApp,
    string AccessMemberType,
    string AccessMemberTier
)
{
    /// <summary>Minimal constructor for backward-compatible callers that only need tree fields (US-13 wizard).</summary>
    public CatalogCategoryRow(int id, int? parentId, string name, string slug, string path, int depth, int sortOrder)
        : this(
            Id: id, TenantId: "", ParentId: parentId, Path: path, Depth: depth,
            Name: name, Slug: slug, SortOrder: sortOrder,
            Active: true, PublishFrom: null, PublishUntil: null,
            ImageMenuUrl: "", ImagePageUrl: "", ImageThumbUrl: "",
            ShowInNav: true, ShowInBrands: false, CustomNavUrl: "",
            NavSortPriority: 10, BreakNavColumn: false,
            DefaultSort: "bestseller", NoRecommendations: false,
            MetaTitleJson: "{}", MetaKeywordsJson: "{}", MetaDescJson: "{}",
            RestrictAccess: false, AccessApp: "", AccessMemberType: "", AccessMemberTier: "")
    { }
}
