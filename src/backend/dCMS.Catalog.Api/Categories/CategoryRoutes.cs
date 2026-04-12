using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Catalog.Api.Categories;

/// <summary>US-13: tenant-scoped category list for Umbraco wizard (flat + <c>isLeaf</c>).</summary>
public static class CategoryRoutes
{
    public static void MapCategoryRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/categories")
            .WithTags("catalog-categories")
            .WithTenantStoreAccess(configuration);

        AuthRead(g.MapGet("", ListCategories), auth);
    }

    private static RouteHandlerBuilder AuthRead(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.CatalogRead) : builder;

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
            if (c.ParentId is { } pid)
                idsWithChildren.Add(pid);
        }

        var items = list.Select(c => new
        {
            id = c.Id,
            parentId = c.ParentId,
            name = c.Name,
            slug = c.Slug,
            path = c.Path,
            depth = c.Depth,
            sortOrder = c.SortOrder,
            isLeaf = !idsWithChildren.Contains(c.Id)
        }).ToList();

        return ApiEnvelope.Ok(new { items });
    }
}
