namespace dCMS.Core.Persistence;

public sealed record BestSellerRankingCriteria(
    string TenantId,
    string StoreId,
    string RecommendationLogic,
    int MaxItems,
    int[] WhitelistedCategoryIds,
    int[] BlacklistedCategoryIds,
    string[] WhitelistedBrandIds,
    string[] BlacklistedBrandIds,
    string[] IncludedProductIds,
    string[] ExcludedProductIds,
    string[] ManualProductIds);

public interface IBestSellerRankingPersistence
{
    Task<IReadOnlyList<BestSellerProductRow>> RankAsync(BestSellerRankingCriteria criteria,
        CancellationToken cancellationToken = default);
}
