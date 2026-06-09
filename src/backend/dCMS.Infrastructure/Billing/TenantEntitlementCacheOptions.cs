namespace dCMS.Infrastructure.Billing;

public sealed class TenantEntitlementCacheOptions
{
    public const string SectionName = "Dcms:Billing:EntitlementCache";

    public TimeSpan RedisTtl { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan MemoryTtl { get; set; } = TimeSpan.FromSeconds(30);

    public string KeyPrefix { get; set; } = "dcms:tenant:entitlements";

    public TimeSpan VersionMemoryTtl { get; set; } = TimeSpan.FromSeconds(2);
}
