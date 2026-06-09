using dCMS.Provisioning.Domain;

namespace dCMS.Tools.SpawnTenant;

/// <summary>
/// Shared context threaded through the provisioning step pipeline.
/// Credentials are held here but NEVER written to logs or audit entries.
/// </summary>
public sealed class ProvisioningContext
{
    public string TenantId { get; init; } = "";
    public string TenantCode { get; init; } = "";
    public string PlanTier { get; init; } = "starter";
    public Guid RunId { get; init; } = Guid.NewGuid();
    public string SqlServerSaConnectionString { get; init; } = "";
    public string UmbracoPlatformConnectionString { get; init; } = "";
    public string CatalogConnectionString { get; init; } = "";
    public string RedisConnectionString { get; init; } = "";
    public string UmbracoDbName => $"dcms_tenant_{TenantCode}";
    public string AdminEmail { get; init; } = "";
    public string AdminPassword { get; init; } = "";
    public string PrimaryDomain { get; init; } = "";
    public string DefaultStoreId { get; init; } = "default";
    public string EnvFilePath { get; init; } = "";
    public string ComposeProject { get; init; } = "";
    public bool ComposeUp { get; init; }
    public string? HealthUrl { get; init; }
    public int DefaultTrialDays { get; init; } = 14;
    public string PlatformTenantName { get; init; } = "";
    public string ClientId { get; init; } = "default";
    public Dictionary<string, string> Checkpoints { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object> StepArtifacts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    public string Actor { get; init; } = "cli:system";

    public bool IsStepCompleted(string stepName)
        => Checkpoints.TryGetValue(stepName, out var v) && v == "completed";

    public void MarkStepCompleted(string stepName)
        => Checkpoints[stepName] = "completed";

    public override string ToString()
        => $"TenantId={TenantId} Code={TenantCode} Plan={PlanTier} RunId={RunId:N} Actor={Actor}";
}
