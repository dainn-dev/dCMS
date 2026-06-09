using FluentAssertions;

namespace dCMS.Tests.Unit.Access;

/// <summary>SaaS Core — static guard that sensitive routes reference expected authorization policies.</summary>
public sealed class SaasCorePolicyAuditTests
{
    private static string BackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "dCMS.Infrastructure");
            if (Directory.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend source root from test output directory.");
    }

    public static TheoryData<string, string> PolicyExpectations => new()
    {
        { "dCMS.Catalog.Api/Imports/ImportJobRoutes.cs", "DcmsPolicies.CatalogImport" },
        { "dCMS.Order.Api/Routes/OrderDlqAdminRoutes.cs", "DcmsPolicies.OrderDlqAdmin" },
        { "dCMS.Approval.Api/Routes/ApprovalRoutes.cs", "DcmsPolicies.ApprovalManage" },
        { "dCMS.Reports.Api/Routes/ReportsRoutes.cs", "DcmsPolicies.OrderAccess" },
        { "dCMS.Catalog.Api/Products/ProductRoutes.cs", "DcmsPolicies.CatalogWrite" },
        { "dCMS.Catalog.Api/Products/ProductRoutes.cs", "DcmsPolicies.CatalogApproval" },
        { "dCMS.Inventory.Api/Stock/StockRoutes.cs", "DcmsPolicies.InventoryWrite" },
        { "dCMS.Order.Api/Routes/OrderFailedRoutes.cs", "DcmsPolicies.OrderFailureManage" },
    };

    [Theory]
    [MemberData(nameof(PolicyExpectations))]
    public void Sensitive_route_file_references_expected_policy(string relativePath, string policyConstant)
    {
        var fullPath = Path.Combine(BackendSrcRoot(), relativePath);
        File.Exists(fullPath).Should().BeTrue($"expected route file at {fullPath}");
        var content = File.ReadAllText(fullPath);
        content.Should().Contain(policyConstant, because: $"{relativePath} should enforce {policyConstant}");
    }
}
