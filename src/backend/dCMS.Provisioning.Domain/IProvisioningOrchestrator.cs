namespace dCMS.Provisioning.Domain;

/// <summary>Provisioning lifecycle orchestration (CLI implementation in SpawnTenant).</summary>
public interface IProvisioningOrchestrator
{
    Task<ProvisioningResult> ProvisionAsync(ProvisioningRequest request, CancellationToken cancellationToken = default);

    Task<ProvisioningResult> RetryAsync(ProvisioningRequest request, CancellationToken cancellationToken = default);

    Task<ProvisioningResult> RollbackAsync(ProvisioningRequest request, bool force, CancellationToken cancellationToken = default);

    Task<ProvisioningResult> SuspendAsync(ProvisioningRequest request, CancellationToken cancellationToken = default);

    Task<ProvisioningResult> ReactivateAsync(ProvisioningRequest request, CancellationToken cancellationToken = default);
}

public sealed record ProvisioningRequest(
    string TenantId,
    string TenantCode,
    string PlanTier,
    string SqlServerSaConnectionString,
    string CatalogConnectionString,
    string UmbracoPlatformConnectionString,
    string RedisConnectionString,
    string AdminEmail,
    string AdminPassword,
    string? PrimaryDomain,
    string DefaultStoreId,
    string EnvFilePath,
    string ComposeProject,
    string Actor,
    bool ComposeUp,
    string? HealthUrl,
    int DefaultTrialDays = 14);

public sealed record ProvisioningResult(
    bool Success,
    string TenantId,
    ProvisioningStatus Status,
    Guid? RunId,
    string? Message);
