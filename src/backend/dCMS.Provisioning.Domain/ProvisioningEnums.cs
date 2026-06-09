namespace dCMS.Provisioning.Domain;

public enum ProvisioningStepStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped,
    RolledBack
}

public static class ProvisioningStepStatusExtensions
{
    public static string ToDbString(this ProvisioningStepStatus status) => status switch
    {
        ProvisioningStepStatus.Pending => "pending",
        ProvisioningStepStatus.Running => "running",
        ProvisioningStepStatus.Succeeded => "succeeded",
        ProvisioningStepStatus.Failed => "failed",
        ProvisioningStepStatus.Skipped => "skipped",
        ProvisioningStepStatus.RolledBack => "rolled_back",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ProvisioningStepStatus FromDbString(string dbValue) => dbValue.ToLowerInvariant() switch
    {
        "pending" => ProvisioningStepStatus.Pending,
        "running" => ProvisioningStepStatus.Running,
        "succeeded" => ProvisioningStepStatus.Succeeded,
        "failed" => ProvisioningStepStatus.Failed,
        "skipped" => ProvisioningStepStatus.Skipped,
        "rolled_back" => ProvisioningStepStatus.RolledBack,
        _ => throw new ArgumentException($"Invalid step status: {dbValue}", nameof(dbValue))
    };
}

public enum OnboardingItemStatus
{
    Pending,
    Completed,
    Skipped
}

public static class OnboardingItemStatusExtensions
{
    public static string ToDbString(this OnboardingItemStatus status) => status switch
    {
        OnboardingItemStatus.Pending => "pending",
        OnboardingItemStatus.Completed => "completed",
        OnboardingItemStatus.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static OnboardingItemStatus FromDbString(string dbValue) => dbValue.ToLowerInvariant() switch
    {
        "pending" => OnboardingItemStatus.Pending,
        "completed" => OnboardingItemStatus.Completed,
        "skipped" => OnboardingItemStatus.Skipped,
        _ => throw new ArgumentException($"Invalid onboarding status: {dbValue}", nameof(dbValue))
    };
}

public enum DomainBindingStatus
{
    Pending,
    Active,
    Suspended,
    Removed
}

public static class DomainBindingStatusExtensions
{
    public static string ToDbString(this DomainBindingStatus status) => status switch
    {
        DomainBindingStatus.Pending => "pending",
        DomainBindingStatus.Active => "active",
        DomainBindingStatus.Suspended => "suspended",
        DomainBindingStatus.Removed => "removed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static DomainBindingStatus FromDbString(string dbValue) => dbValue.ToLowerInvariant() switch
    {
        "pending" => DomainBindingStatus.Pending,
        "active" => DomainBindingStatus.Active,
        "suspended" => DomainBindingStatus.Suspended,
        "removed" => DomainBindingStatus.Removed,
        _ => throw new ArgumentException($"Invalid domain binding status: {dbValue}", nameof(dbValue))
    };
}

public enum RollbackStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public static class RollbackStatusExtensions
{
    public static string ToDbString(this RollbackStatus status) => status switch
    {
        RollbackStatus.Pending => "pending",
        RollbackStatus.Running => "running",
        RollbackStatus.Succeeded => "succeeded",
        RollbackStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static RollbackStatus FromDbString(string dbValue) => dbValue.ToLowerInvariant() switch
    {
        "pending" => RollbackStatus.Pending,
        "running" => RollbackStatus.Running,
        "succeeded" => RollbackStatus.Succeeded,
        "failed" => RollbackStatus.Failed,
        _ => throw new ArgumentException($"Invalid rollback status: {dbValue}", nameof(dbValue))
    };
}
