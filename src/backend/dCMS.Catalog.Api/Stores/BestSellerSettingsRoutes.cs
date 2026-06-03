using System.Security.Claims;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using dCMS.Core.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Stores;

/// <summary>
/// Per-store Best Seller widget configuration (eStore → Products → Best Seller Settings).
/// Route group: /api/v1/tenants/{tenantId}/stores/{storeId}/best-seller-settings
/// </summary>
public static class BestSellerSettingsRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<int> AllowedDurations = [7, 14, 30, 60, 90];
    private static readonly HashSet<int> AllowedMaxItems = [4, 8, 12, 16];
    private static readonly HashSet<string> AllowedLogic =
        new(StringComparer.OrdinalIgnoreCase) { "sales-quantity", "sales-amount", "views", "manual" };

    public static void MapBestSellerSettingsRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/best-seller-settings")
            .WithTags("catalog-best-seller-settings")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("", GetSettings), auth, write: false);
        Auth(g.MapPut("", PutSettings), auth, write: true);
        Auth(g.MapGet("history", GetHistory), auth, write: false);
        Auth(g.MapGet("preview", PreviewRanking), auth, write: false);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled
            ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : b;

    private static string ActorUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

    private static string ActorCatalogRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(DcmsRoles.SuperAdmin)) return DcmsRoles.SuperAdmin;
        if (user.IsInRole(DcmsRoles.ChainAdmin)) return DcmsRoles.ChainAdmin;
        if (user.IsInRole(DcmsRoles.BrandManager)) return DcmsRoles.BrandManager;
        if (user.IsInRole(DcmsRoles.StoreManager)) return DcmsRoles.StoreManager;
        if (user.IsInRole(DcmsRoles.StoreStaff)) return DcmsRoles.StoreStaff;
        return "user";
    }

    public sealed record BestSellerSettingsDto(
        bool DisplayList,
        int PopularityDurationDays,
        bool GenderBased,
        string RecommendationLogic,
        int MaxItems,
        int[]? WhitelistedCategoryIds,
        int[]? BlacklistedCategoryIds,
        string[]? WhitelistedBrandIds,
        string[]? BlacklistedBrandIds,
        string[]? IncludedProductIds,
        string[]? ExcludedProductIds,
        string[]? ManualProductIds);

    private sealed record PutRequest(BestSellerSettingsDto? Settings);

    private static BestSellerSettingsDto Defaults() => new(
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

    private static async Task<IResult> GetSettings(
        string tenantId,
        string storeId,
        IStoreBestSellerSettingsPersistence store,
        CancellationToken cancellationToken)
    {
        var (json, updatedAt) = await store
            .GetSettingsWithUpdatedAtAsync(tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            return ApiEnvelope.Ok(new { configured = false, settings = Defaults(), updatedAt = (DateTimeOffset?)null });

        try
        {
            var settings = JsonSerializer.Deserialize<BestSellerSettingsDto>(json, Json) ?? Defaults();
            return ApiEnvelope.Ok(new { configured = true, settings, updatedAt });
        }
        catch (JsonException)
        {
            return ApiEnvelope.Ok(new { configured = false, settings = Defaults(), updatedAt });
        }
    }

    private static async Task<IResult> PutSettings(
        HttpContext http,
        string tenantId,
        string storeId,
        [FromBody] PutRequest body,
        IStoreBestSellerSettingsPersistence store,
        CancellationToken cancellationToken)
    {
        if (body.Settings is null)
            return ApiEnvelope.Error("validation_error", "Settings body is required.", StatusCodes.Status400BadRequest);

        if (!Validate(body.Settings, out var error, out var normalized))
            return ApiEnvelope.Error("validation_error", error!, StatusCodes.Status400BadRequest);

        var now = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(normalized, Json);
        await store.UpsertSettingsJsonAsync(tenantId, storeId, json, now, cancellationToken).ConfigureAwait(false);
        await store.InsertHistoryAsync(tenantId, storeId, json, ActorUserId(http.User), ActorCatalogRole(http.User),
            now, cancellationToken).ConfigureAwait(false);

        return ApiEnvelope.Ok(new { configured = true, settings = normalized, updatedAt = now });
    }

    private static async Task<IResult> GetHistory(
        string tenantId,
        string storeId,
        IStoreBestSellerSettingsPersistence store,
        int? limit,
        CancellationToken cancellationToken)
    {
        var rows = await store.ListHistoryAsync(tenantId, storeId, limit ?? 20, cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(r =>
        {
            BestSellerSettingsDto? settings = null;
            try
            {
                settings = JsonSerializer.Deserialize<BestSellerSettingsDto>(r.SettingsJson, Json);
            }
            catch (JsonException)
            {
                // ignore malformed snapshot
            }

            return new
            {
                id = r.Id,
                userId = r.UserId,
                userRole = r.UserRole,
                createdAt = r.CreatedAt,
                settings
            };
        }).ToList();

        return ApiEnvelope.Ok(items, new { count = items.Count });
    }

    private static async Task<IResult> PreviewRanking(
        string tenantId,
        string storeId,
        BestSellerRankingService ranking,
        CancellationToken cancellationToken)
    {
        var (cfg, updatedAt) = await ranking.LoadSettingsAsync(tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        var rows = await ranking.ResolveAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var items = rows.Select(MapProductRow).ToList();
        return ApiEnvelope.Ok(new { settings = MapSettings(cfg), updatedAt, items });
    }

    internal static object MapProductRow(BestSellerProductRow row)
    {
        var nameByLocale = ParseNameMap(row.NameJson);
        return new
        {
            id = row.Id,
            name = MultilangJson.PickDisplayName(row.NameJson),
            nameByLocale,
            slug = row.Slug,
            categoryId = row.CategoryId,
            brandId = row.BrandId,
            salesCount30d = row.SalesCount30d,
            pageViews30d = row.PageViews30d,
            minBasePrice = new { amount = row.MinBasePriceAmount, currency = "VND" }
        };
    }

    private static object MapSettings(BestSellerRankingService.ResolvedSettings cfg) => new
    {
        configured = cfg.Configured,
        displayList = cfg.DisplayList,
        popularityDurationDays = cfg.PopularityDurationDays,
        genderBased = cfg.GenderBased,
        recommendationLogic = cfg.RecommendationLogic,
        maxItems = cfg.MaxItems,
        whitelistedCategoryIds = cfg.WhitelistedCategoryIds,
        blacklistedCategoryIds = cfg.BlacklistedCategoryIds,
        whitelistedBrandIds = cfg.WhitelistedBrandIds,
        blacklistedBrandIds = cfg.BlacklistedBrandIds,
        includedProductIds = cfg.IncludedProductIds,
        excludedProductIds = cfg.ExcludedProductIds,
        manualProductIds = cfg.ManualProductIds
    };

    private static Dictionary<string, string> ParseNameMap(string nameJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(nameJson, Json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static bool Validate(BestSellerSettingsDto input, out string? error, out BestSellerSettingsDto normalized)
    {
        error = null;
        normalized = Defaults();

        if (!AllowedDurations.Contains(input.PopularityDurationDays))
        {
            error = "Popularity duration must be 7, 14, 30, 60 or 90 days.";
            return false;
        }

        if (!AllowedMaxItems.Contains(input.MaxItems))
        {
            error = "Max items must be 4, 8, 12 or 16.";
            return false;
        }

        var logic = (input.RecommendationLogic ?? "").Trim().ToLowerInvariant();
        if (!AllowedLogic.Contains(logic))
        {
            error = "Recommendation logic must be sales-quantity, sales-amount, views or manual.";
            return false;
        }

        static int[] cleanIds(int[]? ids) =>
            (ids ?? []).Where(i => i > 0).Distinct().ToArray();

        static string[] cleanStrings(string[]? ids) =>
            (ids ?? []).Select(s => (s ?? "").Trim()).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

        var whitelistCats = cleanIds(input.WhitelistedCategoryIds);
        var blacklistCats = cleanIds(input.BlacklistedCategoryIds);
        if (whitelistCats.Intersect(blacklistCats).Any())
        {
            error = "A category cannot appear in both whitelist and blacklist.";
            return false;
        }

        var whitelistBrands = cleanStrings(input.WhitelistedBrandIds);
        var blacklistBrands = cleanStrings(input.BlacklistedBrandIds);
        if (whitelistBrands.Intersect(blacklistBrands, StringComparer.Ordinal).Any())
        {
            error = "A brand cannot appear in both whitelist and blacklist.";
            return false;
        }

        var included = cleanStrings(input.IncludedProductIds);
        var excluded = cleanStrings(input.ExcludedProductIds);
        if (included.Intersect(excluded, StringComparer.Ordinal).Any())
        {
            error = "A product cannot appear in both included and excluded lists.";
            return false;
        }

        var manual = cleanStrings(input.ManualProductIds);
        if (manual.Intersect(excluded, StringComparer.Ordinal).Any())
        {
            error = "A product cannot appear in both manual selection and excluded lists.";
            return false;
        }

        if (logic == "manual" && manual.Length == 0)
        {
            error = "Manual selection requires at least one product in manualProductIds.";
            return false;
        }

        normalized = new BestSellerSettingsDto(
            input.DisplayList,
            input.PopularityDurationDays,
            input.GenderBased,
            logic,
            input.MaxItems,
            whitelistCats,
            blacklistCats,
            whitelistBrands,
            blacklistBrands,
            included,
            excluded,
            manual);
        return true;
    }
}
