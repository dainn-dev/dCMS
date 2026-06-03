using System.Security.Claims;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Stores;

/// <summary>
/// Per-store product quantity limit configuration.
/// Route group: /api/v1/tenants/{tenantId}/stores/{storeId}/quantity-limit-settings
/// </summary>
public static class QuantityLimitSettingsRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedLimitTypes =
        new(StringComparer.OrdinalIgnoreCase) { "per_cart", "per_user" };

    public static void MapQuantityLimitSettingsRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/quantity-limit-settings")
            .WithTags("catalog-quantity-limit-settings")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("", GetAll), auth, write: false);
        Auth(g.MapPut("", PutGeneral), auth, write: true);
        Auth(g.MapPost("rules", CreateRule), auth, write: true);
        Auth(g.MapPut("rules/{ruleId}", UpdateRule), auth, write: true);
        Auth(g.MapDelete("rules/{ruleId}", DeleteRule), auth, write: true);
        Auth(g.MapGet("history", GetHistory), auth, write: false);
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

    public sealed record RuleDto(
        string? Id,
        string Name,
        string LimitType,
        bool PerProduct,
        int QuantityLimit,
        string StartDate,
        string? EndDate,
        string? BrandId,
        int[]? CategoryIds,
        string? ProductId,
        string? MembershipType,
        string? MembershipTier);

    private sealed record PutGeneralRequest(int CartLimitPerProduct);
    private sealed record RuleBody(RuleDto? Rule);

    private static async Task<IResult> GetAll(
        string tenantId,
        string storeId,
        IStoreQuantityLimitPersistence store,
        CancellationToken cancellationToken)
    {
        var general = await store.GetGeneralAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var rules = await store.ListRulesAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new
        {
            cartLimitPerProduct = general.CartLimitPerProduct,
            updatedAt = general.UpdatedAt,
            rules = rules.Select(MapRuleDto).ToList()
        });
    }

    private static async Task<IResult> PutGeneral(
        HttpContext http,
        string tenantId,
        string storeId,
        [FromBody] PutGeneralRequest body,
        IStoreQuantityLimitPersistence store,
        CancellationToken cancellationToken)
    {
        if (body.CartLimitPerProduct <= 0)
            return ApiEnvelope.Error("validation_error", "Cart limit per product must be greater than zero.",
                StatusCodes.Status400BadRequest);

        var now = DateTimeOffset.UtcNow;
        await store.UpsertGeneralAsync(tenantId, storeId, body.CartLimitPerProduct, now, cancellationToken)
            .ConfigureAwait(false);
        await AppendHistoryAsync(store, tenantId, storeId, http, "update_general",
            new { cartLimitPerProduct = body.CartLimitPerProduct }, now, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { cartLimitPerProduct = body.CartLimitPerProduct, updatedAt = now });
    }

    private static async Task<IResult> CreateRule(
        HttpContext http,
        string tenantId,
        string storeId,
        [FromBody] RuleBody body,
        IStoreQuantityLimitPersistence store,
        CancellationToken cancellationToken)
    {
        if (body.Rule is null)
            return ApiEnvelope.Error("validation_error", "Rule body is required.", StatusCodes.Status400BadRequest);

        if (!TryNormalizeRule(body.Rule, out var normalized, out var error))
            return ApiEnvelope.Error("validation_error", error!, StatusCodes.Status400BadRequest);

        var now = DateTimeOffset.UtcNow;
        var row = new StoreQuantityLimitRuleRow(
            Id: "qtl_" + Guid.NewGuid().ToString("N"),
            TenantId: tenantId,
            StoreId: storeId,
            Name: normalized.Name,
            LimitType: normalized.LimitType,
            PerProduct: normalized.PerProduct,
            QuantityLimit: normalized.QuantityLimit,
            StartDate: normalized.StartDate,
            EndDate: normalized.EndDate,
            BrandId: normalized.BrandId,
            CategoryIds: normalized.CategoryIds,
            ProductId: normalized.ProductId,
            MembershipType: normalized.MembershipType,
            MembershipTier: normalized.MembershipTier,
            ModifiedBy: ActorUserId(http.User),
            CreatedAt: now,
            UpdatedAt: now);

        await store.InsertRuleAsync(row, cancellationToken).ConfigureAwait(false);
        await AppendHistoryAsync(store, tenantId, storeId, http, "create_rule", MapRuleDto(row), now, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(MapRuleDto(row));
    }

    private static async Task<IResult> UpdateRule(
        HttpContext http,
        string tenantId,
        string storeId,
        string ruleId,
        [FromBody] RuleBody body,
        IStoreQuantityLimitPersistence store,
        CancellationToken cancellationToken)
    {
        if (body.Rule is null)
            return ApiEnvelope.Error("validation_error", "Rule body is required.", StatusCodes.Status400BadRequest);

        var existing = await store.GetRuleAsync(tenantId, storeId, ruleId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", "Rule not found.", StatusCodes.Status404NotFound);

        if (!TryNormalizeRule(body.Rule, out var normalized, out var error))
            return ApiEnvelope.Error("validation_error", error!, StatusCodes.Status400BadRequest);

        var now = DateTimeOffset.UtcNow;
        var row = existing with
        {
            Name = normalized.Name,
            LimitType = normalized.LimitType,
            PerProduct = normalized.PerProduct,
            QuantityLimit = normalized.QuantityLimit,
            StartDate = normalized.StartDate,
            EndDate = normalized.EndDate,
            BrandId = normalized.BrandId,
            CategoryIds = normalized.CategoryIds,
            ProductId = normalized.ProductId,
            MembershipType = normalized.MembershipType,
            MembershipTier = normalized.MembershipTier,
            ModifiedBy = ActorUserId(http.User),
            UpdatedAt = now
        };

        var ok = await store.UpdateRuleAsync(row, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return ApiEnvelope.Error("not_found", "Rule not found.", StatusCodes.Status404NotFound);

        await AppendHistoryAsync(store, tenantId, storeId, http, "update_rule", MapRuleDto(row), now, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(MapRuleDto(row));
    }

    private static async Task<IResult> DeleteRule(
        HttpContext http,
        string tenantId,
        string storeId,
        string ruleId,
        IStoreQuantityLimitPersistence store,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetRuleAsync(tenantId, storeId, ruleId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", "Rule not found.", StatusCodes.Status404NotFound);

        var ok = await store.DeleteRuleAsync(tenantId, storeId, ruleId, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return ApiEnvelope.Error("not_found", "Rule not found.", StatusCodes.Status404NotFound);

        var now = DateTimeOffset.UtcNow;
        await AppendHistoryAsync(store, tenantId, storeId, http, "delete_rule",
            new { id = ruleId, rule = MapRuleDto(existing) }, now, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { deleted = true, id = ruleId });
    }

    private static async Task<IResult> GetHistory(
        string tenantId,
        string storeId,
        IStoreQuantityLimitPersistence store,
        int? limit,
        CancellationToken cancellationToken)
    {
        var rows = await store.ListHistoryAsync(tenantId, storeId, limit ?? 20, cancellationToken)
            .ConfigureAwait(false);
        var items = rows.Select(r =>
        {
            object? snapshot = null;
            try
            {
                snapshot = JsonSerializer.Deserialize<object>(r.SnapshotJson, Json);
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
                action = r.Action,
                createdAt = r.CreatedAt,
                snapshot
            };
        }).ToList();
        return ApiEnvelope.Ok(items, new { count = items.Count });
    }

    private static Task AppendHistoryAsync(
        IStoreQuantityLimitPersistence store,
        string tenantId,
        string storeId,
        HttpContext http,
        string action,
        object snapshot,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken) =>
        store.InsertHistoryAsync(tenantId, storeId, action,
            JsonSerializer.Serialize(snapshot, Json),
            ActorUserId(http.User),
            ActorCatalogRole(http.User),
            createdAt,
            cancellationToken);

    private sealed record NormalizedRule(
        string Name,
        string LimitType,
        bool PerProduct,
        int QuantityLimit,
        DateOnly StartDate,
        DateOnly? EndDate,
        string? BrandId,
        int[] CategoryIds,
        string? ProductId,
        string? MembershipType,
        string? MembershipTier);

    private static bool TryNormalizeRule(RuleDto input, out NormalizedRule normalized, out string? error)
    {
        normalized = null!;
        error = null;

        var name = (input.Name ?? "").Trim();
        if (name.Length == 0)
        {
            error = "Name is required.";
            return false;
        }

        if (name.Length > 256)
        {
            error = "Name must be at most 256 characters.";
            return false;
        }

        var limitType = (input.LimitType ?? "").Trim().ToLowerInvariant();
        if (!AllowedLimitTypes.Contains(limitType))
        {
            error = "Limit type must be per_cart or per_user.";
            return false;
        }

        if (input.QuantityLimit <= 0)
        {
            error = "Quantity limit must be greater than zero.";
            return false;
        }

        if (!DateOnly.TryParse(input.StartDate, out var startDate))
        {
            error = "Start date is required (YYYY-MM-DD).";
            return false;
        }

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(input.EndDate))
        {
            if (!DateOnly.TryParse(input.EndDate, out var parsedEnd))
            {
                error = "End date must be YYYY-MM-DD when provided.";
                return false;
            }

            endDate = parsedEnd;
            if (endDate < startDate)
            {
                error = "End date cannot be before start date.";
                return false;
            }
        }

        var categoryIds = (input.CategoryIds ?? []).Where(i => i > 0).Distinct().ToArray();
        static string? clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        normalized = new NormalizedRule(
            name,
            limitType,
            input.PerProduct,
            input.QuantityLimit,
            startDate,
            endDate,
            clean(input.BrandId),
            categoryIds,
            clean(input.ProductId),
            clean(input.MembershipType),
            clean(input.MembershipTier));
        return true;
    }

    private static object MapRuleDto(StoreQuantityLimitRuleRow r) => new
    {
        id = r.Id,
        name = r.Name,
        limitType = r.LimitType,
        perProduct = r.PerProduct,
        quantityLimit = r.QuantityLimit,
        startDate = r.StartDate.ToString("yyyy-MM-dd"),
        endDate = r.EndDate?.ToString("yyyy-MM-dd"),
        brandId = r.BrandId,
        categoryIds = r.CategoryIds,
        productId = r.ProductId,
        membershipType = r.MembershipType,
        membershipTier = r.MembershipTier,
        modifiedBy = r.ModifiedBy,
        updatedAt = r.UpdatedAt
    };
}
