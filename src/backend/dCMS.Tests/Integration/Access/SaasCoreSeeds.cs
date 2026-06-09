namespace dCMS.Tests.Integration.Access;

/// <summary>Canonical tenant/store IDs for SaaS Core RBAC and cross-tenant isolation tests.</summary>
public static class SaasCoreSeeds
{
    public const string TenantA = "t-saas-a";
    public const string TenantB = "t-saas-b";
    public const string StoreA1 = "s-saas-a1";
    public const string StoreB1 = "s-saas-b1";
    public const string BrandA1 = "b-saas-a1";

    public const string JwtKey = "integration-test-signing-key-32bytes!!";
    public const string JwtIssuer = "dcms";
    public const string JwtAudience = "dcms-api";
    public const string ClientId = "saas-test-client";
}
