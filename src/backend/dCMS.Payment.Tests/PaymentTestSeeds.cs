namespace dCMS.Payment.Tests;

/// <summary>Canonical IDs for Payment API security and webhook replay integration tests.</summary>
public static class PaymentTestSeeds
{
    public const string ClientId = "saas-test-client";
    public const string OtherClientId = "other-client-id";
    public const string Provider = "stub";
    public const string InternalApiKey = "payment-internal-test-key-32chars!!";
    public const string WebhookSecret = "webhook-test-secret";

    /// <summary>Home tenant (Tenant A).</summary>
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    /// <summary>Foreign tenant (Tenant B).</summary>
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly Guid StoreA1 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid StoreB1 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public static string TenantAStr => TenantA.ToString("D");
    public static string TenantBStr => TenantB.ToString("D");
    public static string StoreA1Str => StoreA1.ToString("D");
    public static string StoreB1Str => StoreB1.ToString("D");
}
