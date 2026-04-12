using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Persistence;
using Microsoft.Extensions.Configuration;

namespace dCMS.Catalog.Api.Stores;

public static class StoreCatalogSettingsRoutes
{
    public static void MapStoreCatalogSettingsRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/store-catalog-settings")
            .WithTags("catalog-store-settings")
            .WithTenantStoreAccess(configuration);

        Auth(g.MapGet("", GetSettings), auth, write: false);
        Auth(g.MapPatch("", PatchSettings), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder builder, bool authEnabled, bool write) =>
        authEnabled
            ? builder.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : builder;

    private static async Task<IResult> GetSettings(
        string tenantId,
        string storeId,
        ICatalogPersistence persistence,
        CancellationToken cancellationToken)
    {
        var row = await persistence.GetStoreCatalogSettingsAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return ApiEnvelope.Ok(new
            {
                approvalRequired = false,
                lowStockThreshold = (int?)null,
                updatedAt = (DateTimeOffset?)null
            });
        }

        return ApiEnvelope.Ok(new
        {
            approvalRequired = row.ApprovalRequired,
            lowStockThreshold = row.LowStockThreshold,
            updatedAt = row.UpdatedAt
        });
    }

    private static async Task<IResult> PatchSettings(
        string tenantId,
        string storeId,
        PatchStoreCatalogSettingsBody body,
        ICatalogPersistence persistence,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await persistence.GetStoreCatalogSettingsAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var approval = body.ApprovalRequired ?? existing?.ApprovalRequired ?? false;
        var low = body.LowStockThreshold ?? existing?.LowStockThreshold;
        if (low is < 0)
            return ApiEnvelope.Error("validation_error", "LowStockThreshold must be null or non-negative.",
                StatusCodes.Status400BadRequest);

        await persistence.UpsertStoreCatalogSettingsAsync(tenantId, storeId, approval, low, now, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(new { approvalRequired = approval, lowStockThreshold = low, updatedAt = now });
    }

    public sealed record PatchStoreCatalogSettingsBody(bool? ApprovalRequired, int? LowStockThreshold);
}
