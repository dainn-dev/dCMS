namespace dCMS.Provisioning.Domain;

/// <summary>Default onboarding checklist seeded for each new tenant provisioning request.</summary>
public static class OnboardingChecklist
{
    public static readonly IReadOnlyList<(string CheckItem, bool IsRequired)> DefaultItems =
    [
        ("admin_login_verified", true),
        ("umbraco_content_synced", true),
        ("first_brand_created", true),
        ("first_store_created", true),
        ("domain_configured", true),
        ("smoke_test_passed", true),
        ("first_product_created", false),
        ("payment_gateway_configured", false),
        ("inventory_warehouse_created", false),
        ("smtp_configured", false),
        ("first_order_placed", false),
    ];
}
