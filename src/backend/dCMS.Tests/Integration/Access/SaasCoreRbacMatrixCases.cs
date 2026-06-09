using System.Net;
using dCMS.AspNetCore.Auth;

namespace dCMS.Tests.Integration.Access;

/// <summary>Matrix rows mirrored from docs/operations/saas-core-rbac-matrix.md for parametrized tests.</summary>
public static class SaasCoreRbacMatrixCases
{
    public static IEnumerable<object[]> PromotionsCases()
    {
        yield return Row("promotions", "GET", "/api/v1/tenants/{tenantId}/campaigns",
            null, SaasCoreSeeds.TenantA, SaasCoreSeeds.TenantA, null, HttpStatusCode.Unauthorized, null);

        yield return Row("promotions", "GET", "/api/v1/tenants/{tenantId}/campaigns",
            DcmsRoles.ChainAdmin, SaasCoreSeeds.TenantA, SaasCoreSeeds.TenantB, null,
            HttpStatusCode.Forbidden, "tenant_mismatch");

        yield return Row("promotions", "GET", "/api/v1/tenants/{tenantId}/campaigns",
            DcmsRoles.ChainAdmin, SaasCoreSeeds.TenantA, SaasCoreSeeds.TenantA, null,
            HttpStatusCode.OK, null);

        yield return Row("promotions", "GET", "/api/v1/tenants/{tenantId}/campaigns",
            DcmsRoles.SuperAdmin, SaasCoreSeeds.TenantA, SaasCoreSeeds.TenantB, null,
            HttpStatusCode.OK, null);
    }

    private static object[] Row(
        string service,
        string method,
        string pathTemplate,
        string? role,
        string tokenTenant,
        string routeTenant,
        string? storeHeader,
        HttpStatusCode expected,
        string? errorCode) =>
    [
        new SaasCoreAccessCase(service, method, pathTemplate, role, tokenTenant, routeTenant,
            storeHeader, expected, errorCode),
    ];
}
