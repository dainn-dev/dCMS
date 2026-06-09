namespace dCMS.Provisioning.Domain;

/// <summary>
/// Provisioning lifecycle state machine. Source of truth: TenantProvisioning.Status column.
/// </summary>
public enum ProvisioningStatus
{
    Requested,
    Provisioning,
    Failing,
    Retrying,
    Active,
    Suspended,
    Rollback,
    Deprovisioned
}

/// <summary>Valid state transitions. Maps from-state → allowed to-states.</summary>
public static class ProvisioningStatusTransitions
{
    private static readonly Dictionary<ProvisioningStatus, ProvisioningStatus[]> ValidTransitions = new()
    {
        [ProvisioningStatus.Requested] = [ProvisioningStatus.Provisioning],
        [ProvisioningStatus.Provisioning] = [ProvisioningStatus.Active, ProvisioningStatus.Failing],
        [ProvisioningStatus.Failing] = [ProvisioningStatus.Retrying, ProvisioningStatus.Rollback],
        [ProvisioningStatus.Retrying] = [ProvisioningStatus.Provisioning, ProvisioningStatus.Failing],
        [ProvisioningStatus.Active] = [ProvisioningStatus.Suspended, ProvisioningStatus.Rollback],
        [ProvisioningStatus.Suspended] = [ProvisioningStatus.Active, ProvisioningStatus.Rollback],
        [ProvisioningStatus.Rollback] = [ProvisioningStatus.Deprovisioned, ProvisioningStatus.Failing],
        [ProvisioningStatus.Deprovisioned] = []
    };

    public static bool CanTransition(ProvisioningStatus from, ProvisioningStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static IReadOnlyList<ProvisioningStatus> GetAllowedTransitions(ProvisioningStatus from)
        => ValidTransitions.TryGetValue(from, out var allowed) ? allowed : [];

    public static string ToDbString(this ProvisioningStatus status) => status switch
    {
        ProvisioningStatus.Requested => "requested",
        ProvisioningStatus.Provisioning => "provisioning",
        ProvisioningStatus.Failing => "failing",
        ProvisioningStatus.Retrying => "retrying",
        ProvisioningStatus.Active => "active",
        ProvisioningStatus.Suspended => "suspended",
        ProvisioningStatus.Rollback => "rollback",
        ProvisioningStatus.Deprovisioned => "deprovisioned",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ProvisioningStatus FromDbString(string dbValue) => dbValue.ToLowerInvariant() switch
    {
        "requested" => ProvisioningStatus.Requested,
        "provisioning" => ProvisioningStatus.Provisioning,
        "failing" => ProvisioningStatus.Failing,
        "retrying" => ProvisioningStatus.Retrying,
        "active" => ProvisioningStatus.Active,
        "suspended" => ProvisioningStatus.Suspended,
        "rollback" => ProvisioningStatus.Rollback,
        "deprovisioned" => ProvisioningStatus.Deprovisioned,
        _ => throw new ArgumentException($"Invalid provisioning status: {dbValue}", nameof(dbValue))
    };

    /// <summary>API-friendly alias: <c>failing</c> → <c>failed</c>.</summary>
    public static string ToApiString(this ProvisioningStatus status) =>
        status == ProvisioningStatus.Failing ? "failed" : status.ToDbString();
}
