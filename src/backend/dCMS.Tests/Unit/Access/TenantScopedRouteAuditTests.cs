using FluentAssertions;

namespace dCMS.Tests.Unit.Access;

/// <summary>
/// DAI-32 — regression guard: every tenant-scoped route group in the called-out services
/// must apply <c>WithTenantAccess</c> or <c>WithTenantStoreAccess</c>.
/// </summary>
public sealed class TenantScopedRouteAuditTests
{
    private static string BackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "dCMS.Fulfillment.Api");
            if (Directory.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend source root from test output directory.");
    }

    public static TheoryData<string> TenantScopedRouteFiles => new()
    {
        "dCMS.Fulfillment.Api/FulfillmentRoutes.cs",
        "dCMS.Approval.Api/Routes/ApprovalRoutes.cs",
        "dCMS.Promotions.Api/Campaigns/CampaignRoutes.cs",
        "dCMS.Promotions.Api/PromoCodes/PromoCodeRoutes.cs",
        "dCMS.Loyalty.Api/Routes/LoyaltyRoutes.cs",
        "dCMS.Voucher.Api/Routes/VoucherRoutes.cs",
        "dCMS.Notification.Api/Routes/NotificationFeedRoutes.cs",
        "dCMS.Catalog.Api/Products/ProductRoutes.cs",
        "dCMS.Catalog.Api/Imports/ImportJobRoutes.cs",
        "dCMS.Inventory.Api/Stock/StockRoutes.cs",
        "dCMS.Reports.Api/Routes/ReportsRoutes.cs",
        "dCMS.Order.Api/Routes/OrderHttpRoutes.cs",
        "dCMS.Order.Api/Routes/OrderReportRoutes.cs",
        "dCMS.Order.Api/Routes/OrderFailedRoutes.cs",
    };

    [Theory]
    [MemberData(nameof(TenantScopedRouteFiles))]
    public void Tenant_route_group_applies_jwt_scope_filter(string relativePath)
    {
        var fullPath = Path.Combine(BackendSrcRoot(), relativePath);
        File.Exists(fullPath).Should().BeTrue($"expected route file at {fullPath}");

        var content = File.ReadAllText(fullPath);
        var isHeaderScoped = content.Contains("WithTenantStoreHeaderAccess(", StringComparison.Ordinal);
        if (!isHeaderScoped)
            content.Should().Contain("{tenantId}", $"route file {relativePath} should define tenant-scoped routes");
        var hasTenantFilter = content.Contains("WithTenantAccess(", StringComparison.Ordinal)
            || content.Contains("WithTenantStoreAccess(", StringComparison.Ordinal)
            || content.Contains("WithTenantStoreHeaderAccess(", StringComparison.Ordinal);
        hasTenantFilter.Should().BeTrue(
            $"route file {relativePath} must call WithTenantAccess, WithTenantStoreAccess, or WithTenantStoreHeaderAccess");
    }

    [Fact]
    public void Internal_catalog_routes_use_api_key_filter_not_jwt_tenant_filter()
    {
        var path = Path.Combine(BackendSrcRoot(), "dCMS.Catalog.Api/Internal/InternalCatalogRoutes.cs");
        var content = File.ReadAllText(path);

        content.Should().Contain("InternalCatalogApiKeyEndpointFilter",
            "cross-service catalog routes are protected by API key, not JWT tenant filters");
        content.Should().NotContain("WithTenantAccess",
            "internal catalog routes must not use JWT tenant route filters");
    }

    [Fact]
    public void Internal_promotions_routes_use_api_key_filter_not_jwt_tenant_filter()
    {
        var path = Path.Combine(BackendSrcRoot(), "dCMS.Promotions.Api/Internal/InternalPromotionsRoutes.cs");
        var content = File.ReadAllText(path);

        content.Should().Contain("InternalPromotionsApiKeyEndpointFilter",
            "cross-service workflow routes are intentionally protected by API key, not JWT tenant filters");
        content.Should().NotContain("WithTenantAccess",
            "internal workflow routes must not use JWT tenant route filters");
    }
}
