namespace dCMS.Provisioning.Domain;

public interface ITenantProvisioningRepository
{
    Task<TenantProvisioningRecord?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<TenantProvisioningRecord?> GetByTenantCodeAsync(string tenantCode, CancellationToken cancellationToken = default);

    Task CreateRequestedAsync(
        string tenantId,
        string tenantCode,
        string planTier,
        string? requestedBy,
        CancellationToken cancellationToken = default);

    Task<bool> TransitionStatusAsync(
        string tenantId,
        ProvisioningStatus from,
        ProvisioningStatus to,
        Guid? runId,
        string? failureMessage,
        string? actor,
        CancellationToken cancellationToken = default);

    Task UpdateInfrastructureAsync(
        string tenantId,
        string? umbracoDbName,
        string? envFilePath,
        string? primaryDomain,
        Guid? currentRunId,
        Guid? lastSuccessfulRunId,
        CancellationToken cancellationToken = default);

    Task SetOnboardingCompleteAsync(string tenantId, bool complete, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvisioningStepRecord>> GetStepsAsync(
        string tenantId,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvisioningStepRecord>> GetSucceededStepsForRunAsync(
        string tenantId,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task UpsertStepAsync(
        string tenantId,
        Guid runId,
        int stepOrder,
        string stepName,
        ProvisioningStepStatus status,
        int attemptCount,
        int maxRetries,
        string? errorMessage,
        string checkpointJson,
        CancellationToken cancellationToken = default);

    Task UpdateStepRollbackAsync(
        long stepId,
        RollbackStatus rollbackStatus,
        string? rollbackErrorMessage,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>> LoadStepCheckpointsAsync(
        string tenantId,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task AppendAuditAsync(
        string tenantId,
        Guid? runId,
        string operation,
        string? fromStatus,
        string? toStatus,
        string? actor,
        string detailsJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvisioningAuditEntry>> ListAuditAsync(
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default);

    Task SeedOnboardingChecklistAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantOnboardingItem>> ListOnboardingAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task UpsertDomainBindingAsync(
        string domain,
        string tenantId,
        string storeId,
        bool isPrimary,
        DomainBindingStatus status,
        string? redisHostKey,
        string? redisKeysWrittenJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantDomainBindingRecord>> ListDomainBindingsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
