namespace dCMS.Tools.SpawnTenant.Steps;

public static class ProvisioningStepNames
{
    public const string ValidateRequest = "validate_request";
    public const string CreatePlatformTenant = "create_platform_tenant";
    public const string CreateUmbracoDb = "create_umbraco_db";
    public const string WriteEnvFile = "write_env_file";
    public const string VerifyDbConnection = "verify_db_connection";
    public const string ComposeUp = "compose_up";
    public const string HealthPoll = "health_poll";
    public const string BindDomain = "bind_domain";
    public const string SeedDefaultStore = "seed_default_store";
    public const string MarkOnboardingPending = "mark_onboarding_pending";
}

public static class ProvisioningCheckpoint
{
    public static string CompletedJson(string? extra = null) =>
        extra is null ? """{"marker":"completed"}""" : $$"""{"marker":"completed","detail":{{System.Text.Json.JsonSerializer.Serialize(extra)}}}""";
}
