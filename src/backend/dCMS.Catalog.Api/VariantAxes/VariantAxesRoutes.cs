using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Catalog.Api.VariantAxes;

/// <summary>US-13 / DAI-284: store-scoped variant axis definitions (attributes + values, optional allowlist).</summary>
public static class VariantAxesRoutes
{
    public static void MapVariantAxesRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/variant-axes")
            .WithTags("catalog-variant-axes")
            .WithTenantStoreAccess(configuration);

        AuthRead(g.MapGet("", ListVariantAxes), auth);
    }

    private static RouteHandlerBuilder AuthRead(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.CatalogRead) : builder;

    private static async Task<IResult> ListVariantAxes(
        string tenantId,
        string storeId,
        ProductService products,
        CancellationToken cancellationToken)
    {
        var axes = await products.ListVariantAxesForStoreAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var items = axes.Select(a => new
        {
            attributeId = a.AttributeId,
            name = a.Name,
            values = a.Values.Select(v => new { id = v.Id, name = v.Name, sortOrder = v.SortOrder }).ToList()
        }).ToList();

        return ApiEnvelope.Ok(new { items });
    }
}
