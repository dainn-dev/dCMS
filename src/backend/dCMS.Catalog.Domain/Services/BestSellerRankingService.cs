using System.Text.Json;
using dCMS.Core.Persistence;

namespace dCMS.Core.Services;

/// <summary>Resolves store best-seller product lists from persisted widget settings.</summary>
public sealed class BestSellerRankingService(
    IStoreBestSellerSettingsPersistence settings,
    IBestSellerRankingPersistence ranking)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record ResolvedSettings(
        bool Configured,
        bool DisplayList,
        int PopularityDurationDays,
        bool GenderBased,
        string RecommendationLogic,
        int MaxItems,
        int[] WhitelistedCategoryIds,
        int[] BlacklistedCategoryIds,
        string[] WhitelistedBrandIds,
        string[] BlacklistedBrandIds,
        string[] IncludedProductIds,
        string[] ExcludedProductIds,
        string[] ManualProductIds);

    public async Task<(ResolvedSettings Settings, DateTimeOffset? UpdatedAt)> LoadSettingsAsync(
        string tenantId, string storeId, CancellationToken cancellationToken = default)
    {
        var (json, updatedAt) = await settings
            .GetSettingsWithUpdatedAtAsync(tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            return (DefaultSettings(configured: false), null);

        try
        {
            var parsed = JsonSerializer.Deserialize<SettingsJson>(json, Json) ?? new SettingsJson();
            return (Map(parsed, configured: true), updatedAt);
        }
        catch (JsonException)
        {
            return (DefaultSettings(configured: false), updatedAt);
        }
    }

    public async Task<IReadOnlyList<BestSellerProductRow>> ResolveAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default)
    {
        var (cfg, _) = await LoadSettingsAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (!cfg.DisplayList)
            return Array.Empty<BestSellerProductRow>();

        return await ranking.RankAsync(new BestSellerRankingCriteria(
            tenantId,
            storeId,
            cfg.RecommendationLogic,
            cfg.MaxItems,
            cfg.WhitelistedCategoryIds,
            cfg.BlacklistedCategoryIds,
            cfg.WhitelistedBrandIds,
            cfg.BlacklistedBrandIds,
            cfg.IncludedProductIds,
            cfg.ExcludedProductIds,
            cfg.ManualProductIds), cancellationToken).ConfigureAwait(false);
    }

    private static ResolvedSettings DefaultSettings(bool configured) => new(
        configured,
        DisplayList: true,
        PopularityDurationDays: 30,
        GenderBased: true,
        RecommendationLogic: "sales-quantity",
        MaxItems: 4,
        WhitelistedCategoryIds: [],
        BlacklistedCategoryIds: [],
        WhitelistedBrandIds: [],
        BlacklistedBrandIds: [],
        IncludedProductIds: [],
        ExcludedProductIds: [],
        ManualProductIds: []);

    private static ResolvedSettings Map(SettingsJson s, bool configured) => new(
        configured,
        s.DisplayList ?? true,
        s.PopularityDurationDays ?? 30,
        s.GenderBased ?? true,
        NormalizeLogic(s.RecommendationLogic),
        s.MaxItems ?? 4,
        s.WhitelistedCategoryIds ?? [],
        s.BlacklistedCategoryIds ?? [],
        s.WhitelistedBrandIds ?? [],
        s.BlacklistedBrandIds ?? [],
        s.IncludedProductIds ?? [],
        s.ExcludedProductIds ?? [],
        s.ManualProductIds ?? []);

    private static string NormalizeLogic(string? logic) =>
        logic?.Trim().ToLowerInvariant() switch
        {
            "sales-amount" or "views" or "manual" => logic.Trim().ToLowerInvariant(),
            _ => "sales-quantity"
        };

    private sealed class SettingsJson
    {
        public bool? DisplayList { get; set; }
        public int? PopularityDurationDays { get; set; }
        public bool? GenderBased { get; set; }
        public string? RecommendationLogic { get; set; }
        public int? MaxItems { get; set; }
        public int[]? WhitelistedCategoryIds { get; set; }
        public int[]? BlacklistedCategoryIds { get; set; }
        public string[]? WhitelistedBrandIds { get; set; }
        public string[]? BlacklistedBrandIds { get; set; }
        public string[]? IncludedProductIds { get; set; }
        public string[]? ExcludedProductIds { get; set; }
        public string[]? ManualProductIds { get; set; }
    }
}
