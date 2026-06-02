using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Categories;

/// <summary>
/// Category API — read list (US-13) + full CRUD management (DAI-586).
/// Read list:  GET /api/v1/tenants/{tenantId}/stores/{storeId}/categories  (Umbraco wizard, store-scoped)
/// Management: /api/v1/tenants/{tenantId}/categories/**                    (tenant-scoped, no storeId)
/// </summary>
public static class CategoryRoutes
{
    public static void MapCategoryRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        // ── Existing: flat list for Umbraco wizard ────────────────────────────
        var wizard = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/categories")
            .WithTags("catalog-categories")
            .WithTenantStoreAccess(configuration);

        Auth(wizard.MapGet("", ListCategories), auth, write: false);

        // ── Management CRUD (tenant-scoped, no storeId) ───────────────────────
        var mgmt = app.MapGroup("/api/v1/tenants/{tenantId}/categories")
            .WithTags("catalog-categories-mgmt")
            .WithTenantAccess(configuration);

        Auth(mgmt.MapGet("",          ListCategoriesMgmt),  auth, write: false);
        Auth(mgmt.MapGet("{id:int}",   GetCategory),         auth, write: false);
        Auth(mgmt.MapPost("",          CreateCategory),      auth, write: true);
        Auth(mgmt.MapPut("{id:int}",   UpdateCategory),      auth, write: true);
        Auth(mgmt.MapDelete("{id:int}", DeleteCategory),     auth, write: true);
        Auth(mgmt.MapPut("{id:int}/parent", ReclassifyCategory), auth, write: true);
        Auth(mgmt.MapPut("sort",       SortCategories),      auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled
            ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : b;

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private static object ToDto(CatalogCategoryRow c) => new
    {
        id             = c.Id,
        tenantId       = c.TenantId,
        parentId       = c.ParentId,
        path           = c.Path,
        depth          = c.Depth,
        name           = c.Name,
        slug           = c.Slug,
        sortOrder      = c.SortOrder,
        code           = c.Code,
        active         = c.Active,
        publishFrom    = c.PublishFrom,
        publishUntil   = c.PublishUntil,
        imageMenuUrl   = c.ImageMenuUrl,
        imagePageUrl   = c.ImagePageUrl,
        imageThumbUrl  = c.ImageThumbUrl,
        showInNav      = c.ShowInNav,
        showInBrands   = c.ShowInBrands,
        customNavUrl   = c.CustomNavUrl,
        navSortPriority= c.NavSortPriority,
        breakNavColumn = c.BreakNavColumn,
        defaultSort    = c.DefaultSort,
        noRecommendations = c.NoRecommendations,
        metaTitleJson  = c.MetaTitleJson,
        metaKeywordsJson = c.MetaKeywordsJson,
        metaDescJson   = c.MetaDescJson,
        restrictAccess = c.RestrictAccess,
        accessApp      = c.AccessApp,
        accessMemberType = c.AccessMemberType,
        accessMemberTier = c.AccessMemberTier,
    };

    private sealed record CategoryWriteRequest(
        string   Name,
        string   Slug,
        int?     ParentId        = null,
        int      SortOrder       = 0,
        string?  Code            = null,
        bool     Active          = true,
        DateTimeOffset? PublishFrom  = null,
        DateTimeOffset? PublishUntil = null,
        string?  ImageMenuUrl    = null,
        string?  ImagePageUrl    = null,
        string?  ImageThumbUrl   = null,
        bool     ShowInNav       = true,
        bool     ShowInBrands    = false,
        string?  CustomNavUrl    = null,
        int      NavSortPriority = 10,
        bool     BreakNavColumn  = false,
        string   DefaultSort     = "bestseller",
        bool     NoRecommendations = false,
        string?  MetaTitleJson   = null,
        string?  MetaKeywordsJson = null,
        string?  MetaDescJson    = null,
        bool     RestrictAccess  = false,
        string?  AccessApp       = null,
        string?  AccessMemberType = null,
        string?  AccessMemberTier = null);

    private sealed record ReclassifyRequest(int? NewParentId);
    private sealed record SortItem(int Id, int SortOrder);
    private sealed record SortRequest(int? ParentId, IReadOnlyList<SortItem> Items);

    // ── Wizard list (existing) ────────────────────────────────────────────────

    private static async Task<IResult> ListCategories(
        string tenantId,
        string storeId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        var list = await products.ListCategoriesForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var idsWithChildren = new HashSet<int>();
        foreach (var c in list)
        {
            if (c.ParentId is { } pid) idsWithChildren.Add(pid);
        }
        var items = list.Select(c => new
        {
            id = c.Id, parentId = c.ParentId, name = c.Name,
            slug = c.Slug, path = c.Path, depth = c.Depth,
            sortOrder = c.SortOrder, isLeaf = !idsWithChildren.Contains(c.Id),
        }).ToList();
        return ApiEnvelope.Ok(new { items });
    }

    // ── Management list ───────────────────────────────────────────────────────

    private static async Task<IResult> ListCategoriesMgmt(
        string tenantId,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        var list = await catalog.ListCategoriesByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var idsWithChildren = new HashSet<int>();
        foreach (var c in list)
            if (c.ParentId is { } pid) idsWithChildren.Add(pid);

        var items = list.Select(c => new
        {
            id = c.Id, tenantId = c.TenantId, parentId = c.ParentId,
            path = c.Path, depth = c.Depth, name = c.Name, slug = c.Slug,
            sortOrder = c.SortOrder, code = c.Code, active = c.Active,
            publishFrom = c.PublishFrom, publishUntil = c.PublishUntil,
            imageMenuUrl = c.ImageMenuUrl, imagePageUrl = c.ImagePageUrl, imageThumbUrl = c.ImageThumbUrl,
            showInNav = c.ShowInNav, showInBrands = c.ShowInBrands, customNavUrl = c.CustomNavUrl,
            navSortPriority = c.NavSortPriority, breakNavColumn = c.BreakNavColumn,
            defaultSort = c.DefaultSort, noRecommendations = c.NoRecommendations,
            metaTitleJson = c.MetaTitleJson, metaKeywordsJson = c.MetaKeywordsJson, metaDescJson = c.MetaDescJson,
            restrictAccess = c.RestrictAccess, accessApp = c.AccessApp,
            accessMemberType = c.AccessMemberType, accessMemberTier = c.AccessMemberTier,
            isLeaf = !idsWithChildren.Contains(c.Id),
        }).ToList();

        return ApiEnvelope.Ok(new { items });
    }

    // ── Get single ────────────────────────────────────────────────────────────

    private static async Task<IResult> GetCategory(
        string tenantId,
        int id,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        var cat = await catalog.GetCategoryByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return cat is null
            ? ApiEnvelope.Error("not_found", $"Category {id} not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(cat));
    }

    // ── Create ────────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateCategory(
        string tenantId,
        [FromBody] CategoryWriteRequest body,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Category name is required.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(body.Slug))
            return ApiEnvelope.Error("validation_error", "Slug is required.", StatusCodes.Status400BadRequest);

        var normalizedSlug = body.Slug.Trim().ToLowerInvariant();
        if (await catalog.CategorySlugExistsAsync(tenantId, normalizedSlug, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Slug '{normalizedSlug}' already exists in this tenant.", StatusCodes.Status409Conflict);

        var row = new CatalogCategoryRow(
            0, tenantId, body.ParentId, "/", 0,
            body.Name.Trim(), normalizedSlug, body.SortOrder,
            (body.Code ?? "").Trim(),
            body.Active, body.PublishFrom, body.PublishUntil,
            body.ImageMenuUrl ?? "", body.ImagePageUrl ?? "", body.ImageThumbUrl ?? "",
            body.ShowInNav, body.ShowInBrands, body.CustomNavUrl ?? "",
            body.NavSortPriority, body.BreakNavColumn,
            body.DefaultSort, body.NoRecommendations,
            body.MetaTitleJson ?? "{}", body.MetaKeywordsJson ?? "{}", body.MetaDescJson ?? "{}",
            body.RestrictAccess, body.AccessApp ?? "", body.AccessMemberType ?? "", body.AccessMemberTier ?? "");

        int newId;
        try { newId = await catalog.CreateCategoryAsync(row, cancellationToken).ConfigureAwait(false); }
        catch (ArgumentException ex)
        { return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest); }

        var created = await catalog.GetCategoryByIdAsync(newId, tenantId, cancellationToken).ConfigureAwait(false);
        return Results.Json(
            new { data = ToDto(created!), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private static async Task<IResult> UpdateCategory(
        string tenantId,
        int id,
        [FromBody] CategoryWriteRequest body,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Category name is required.", StatusCodes.Status400BadRequest);

        var existing = await catalog.GetCategoryByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", $"Category {id} not found.", StatusCodes.Status404NotFound);

        var normalizedSlug = (body.Slug ?? existing.Slug).Trim().ToLowerInvariant();
        if (normalizedSlug != existing.Slug &&
            await catalog.CategorySlugExistsAsync(tenantId, normalizedSlug, id, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Slug '{normalizedSlug}' already exists in this tenant.", StatusCodes.Status409Conflict);

        var updated = existing with
        {
            Name = body.Name.Trim(), Slug = normalizedSlug, SortOrder = body.SortOrder,
            Code = body.Code is null ? existing.Code : body.Code.Trim(),
            Active = body.Active, PublishFrom = body.PublishFrom, PublishUntil = body.PublishUntil,
            ImageMenuUrl = body.ImageMenuUrl ?? existing.ImageMenuUrl,
            ImagePageUrl = body.ImagePageUrl ?? existing.ImagePageUrl,
            ImageThumbUrl = body.ImageThumbUrl ?? existing.ImageThumbUrl,
            ShowInNav = body.ShowInNav, ShowInBrands = body.ShowInBrands,
            CustomNavUrl = body.CustomNavUrl ?? existing.CustomNavUrl,
            NavSortPriority = body.NavSortPriority, BreakNavColumn = body.BreakNavColumn,
            DefaultSort = body.DefaultSort, NoRecommendations = body.NoRecommendations,
            MetaTitleJson = body.MetaTitleJson ?? existing.MetaTitleJson,
            MetaKeywordsJson = body.MetaKeywordsJson ?? existing.MetaKeywordsJson,
            MetaDescJson = body.MetaDescJson ?? existing.MetaDescJson,
            RestrictAccess = body.RestrictAccess,
            AccessApp = body.AccessApp ?? existing.AccessApp,
            AccessMemberType = body.AccessMemberType ?? existing.AccessMemberType,
            AccessMemberTier = body.AccessMemberTier ?? existing.AccessMemberTier,
        };

        await catalog.UpdateCategoryAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private static async Task<IResult> DeleteCategory(
        string tenantId,
        int id,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        var deleted = await catalog.DeleteCategoryAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return deleted
            ? Results.NoContent()
            : ApiEnvelope.Error("not_found", $"Category {id} not found.", StatusCodes.Status404NotFound);
    }

    // ── Reclassify ────────────────────────────────────────────────────────────

    private static async Task<IResult> ReclassifyCategory(
        string tenantId,
        int id,
        [FromBody] ReclassifyRequest body,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        if (body.NewParentId.HasValue && body.NewParentId.Value == id)
            return ApiEnvelope.Error("validation_error", "A category cannot be its own parent.", StatusCodes.Status400BadRequest);

        bool ok;
        try { ok = await catalog.ReclassifyCategoryAsync(id, tenantId, body.NewParentId, cancellationToken).ConfigureAwait(false); }
        catch (ArgumentException ex)
        { return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest); }

        if (!ok)
            return ApiEnvelope.Error("not_found", $"Category {id} not found.", StatusCodes.Status404NotFound);

        var cat = await catalog.GetCategoryByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(cat!));
    }

    // ── Sort ─────────────────────────────────────────────────────────────────

    private static async Task<IResult> SortCategories(
        string tenantId,
        [FromBody] SortRequest body,
        ICatalogPersistence catalog,
        CancellationToken cancellationToken)
    {
        if (body.Items is null || body.Items.Count == 0)
            return ApiEnvelope.Error("validation_error", "Items array is required.", StatusCodes.Status400BadRequest);

        var order = body.Items.Select(i => (i.Id, i.SortOrder)).ToList();
        await catalog.ReorderSiblingsAsync(tenantId, body.ParentId, order, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
