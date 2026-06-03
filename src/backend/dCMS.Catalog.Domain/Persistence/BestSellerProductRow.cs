namespace dCMS.Core.Persistence;

/// <summary>Lightweight product row returned by best-seller ranking queries.</summary>
public sealed record BestSellerProductRow(
    string Id,
    string NameJson,
    string Slug,
    int CategoryId,
    string? BrandId,
    int SalesCount30d,
    int PageViews30d,
    long MinBasePriceAmount);
