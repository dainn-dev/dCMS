namespace dCMS.Core.Persistence;

/// <summary>A single manual recommendation edge resolved with the recommended product's display fields.</summary>
public sealed record ProductRecommendationRow(
    string RecommendedProductId,
    string NameJson,
    string Slug,
    string Status,
    int SortOrder);
