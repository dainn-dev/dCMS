using dCMS.Core.Models;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Tests.Unit.Promotions;

/// <summary>
/// DAI-690: canonical mechanic JSON fixtures and helpers for unit tests.
/// </summary>
internal static class PromotionFixtures
{
    public const string TenantId = "t1";

    public static CampaignRow ActiveCampaign(
        string id,
        string editorKind,
        string mechanicsJson,
        string qualifiersJson = "{}",
        string code = "CAMP",
        DateTimeOffset? createdAt = null) => new(
            Id: id,
            TenantId: TenantId,
            Code: code,
            NameJson: "{\"en\":\"" + code + "\"}",
            EditorKind: editorKind,
            WorkflowState: "active",
            Channel: "Web",
            StartDate: null,
            EndDate: null,
            ActiveDaysJson: "[]",
            ActiveMonthsJson: "[]",
            QualifiersJson: qualifiersJson,
            MechanicsJson: mechanicsJson,
            PromotionDetailsJson: "{}",
            Budget: "0",
            Audience: "all",
            Conversions: 0,
            CreatedAt: createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt: createdAt ?? DateTimeOffset.UtcNow);

    public static CartLine Line(
        string lineId,
        string productId,
        decimal unitPrice,
        int qty = 1,
        string? brandId = null,
        IReadOnlyList<string>? categoryIds = null,
        string? variantId = null,
        string? sku = null) =>
        new(lineId, productId, variantId, sku ?? $"SKU-{productId}", qty, unitPrice,
            categoryIds ?? Array.Empty<string>(), brandId);

    // ── Mechanics JSON helpers ────────────────────────────────────────────────

    public static string ProductDiscountJson(string discountType, decimal value, int? cap = null) =>
        cap is null
            ? $$"""{"discountType":"{{discountType}}","discountValue":{{value}}}"""
            : $$"""{"discountType":"{{discountType}}","discountValue":{{value}},"qualifyingProductsToAvail":{{cap}}}""";

    public static string MixMatchSingleItem(string discountType, decimal value) =>
        $$"""{"mode":"single-item","discountType":"{{discountType}}","discountValue":{{value}}}""";

    public static string MixMatchPerBundle(string discountType, decimal value, int bundleSize) =>
        $$"""{"mode":"per-bundle","discountType":"{{discountType}}","discountValue":{{value}},"bundleSize":{{bundleSize}}}""";

    public static string MixMatchIncremental(string defaultDiscountType, params (int minQty, decimal value)[] tiers)
    {
        var tierJson = string.Join(",",
            tiers.Select(t => $$"""{"minQty":{{t.minQty}},"discountValue":{{t.value}}}"""));
        return $$"""{"mode":"incremental","discountType":"{{defaultDiscountType}}","tiers":[{{tierJson}}]}""";
    }

    public static string QualifyByProducts(params string[] productIds)
    {
        var arr = string.Join(",", productIds.Select(p => "\"" + p + "\""));
        return $$"""{"productIds":[{{arr}}]}""";
    }

    public static string PwpItemJson(int qualifyingPerSet, int maxPerUser, params string[] promoProductIds)
    {
        var arr = string.Join(",", promoProductIds.Select(p => "\"" + p + "\""));
        return $$"""{"qualifyingProductsPerSet":{{qualifyingPerSet}},"maxPromotionalProductsPerUser":{{maxPerUser}},"promotionProductIds":[{{arr}}]}""";
    }

    public static string PwpDiscountJson(int qualifyingPerSet, int maxPerUser,
        string discountType, decimal discountValue, params string[] promoProductIds)
    {
        var arr = string.Join(",", promoProductIds.Select(p => "\"" + p + "\""));
        return $$"""{"qualifyingProductsPerSet":{{qualifyingPerSet}},"maxPromotionalProductsPerUser":{{maxPerUser}},"discountType":"{{discountType}}","discountValue":{{discountValue}},"promotionProductIds":[{{arr}}]}""";
    }

    public static string AfterSalesJson(int qualifyingInCart, int maxSets, string codeType = "standard") =>
        $$"""{"qualifyingProductsInCart":{{qualifyingInCart}},"maxDiscountedProductSets":{{maxSets}},"promotionCodeType":"{{codeType}}"}""";
}
