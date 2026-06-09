namespace dCMS.Provisioning.Domain;

public sealed record TenantProvisioningRecord(
    string TenantId,
    string TenantCode,
    ProvisioningStatus Status,
    string PlanTier,
    string? UmbracoDbName,
    string? EnvFilePath,
    string? PrimaryDomain,
    Guid? CurrentRunId,
    Guid? LastSuccessfulRunId,
    bool OnboardingComplete,
    DateTimeOffset? OnboardingCompletedAt,
    DateTimeOffset RequestedAt,
    string? RequestedBy,
    DateTimeOffset? ProvisioningStartedAt,
    DateTimeOffset? ProvisionedAt,
    DateTimeOffset? SuspendedAt,
    DateTimeOffset? DeprovisionedAt,
    DateTimeOffset UpdatedAt,
    string? LastFailureMessage,
    int FailureCount);

public sealed record ProvisioningStepRecord(
    long Id,
    string TenantId,
    Guid RunId,
    int StepOrder,
    string StepName,
    ProvisioningStepStatus Status,
    int AttemptCount,
    int MaxRetries,
    string? ErrorMessage,
    DateTimeOffset? LastAttemptAt,
    string CheckpointJson,
    RollbackStatus? RollbackStatus,
    DateTimeOffset? RollbackAttemptedAt,
    string? RollbackErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ProvisioningAuditEntry(
    long Id,
    string TenantId,
    Guid? RunId,
    string Operation,
    string? FromStatus,
    string? ToStatus,
    string? Actor,
    string DetailsJson,
    DateTimeOffset CreatedAt);

public sealed record TenantOnboardingItem(
    long Id,
    string TenantId,
    string CheckItem,
    OnboardingItemStatus Status,
    bool IsRequired,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? VerifiedAt,
    string? VerifiedBy,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TenantDomainBindingRecord(
    string Domain,
    string TenantId,
    string StoreId,
    bool IsPrimary,
    DomainBindingStatus Status,
    string? RedisHostKey,
    string? RedisKeysWrittenJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RemovedAt);
