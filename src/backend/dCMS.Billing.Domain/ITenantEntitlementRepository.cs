namespace dCMS.Billing.Domain;

public interface ITenantEntitlementRepository
{
    Task<TenantSubscriptionRecord?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanDefinition>> ListPlansAsync(CancellationToken cancellationToken = default);

    Task<PlanDefinition?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default);

    Task<PlanDefinition?> GetPlanByCodeAsync(PlanCode code, CancellationToken cancellationToken = default);

    Task CreateDefaultTrialSubscriptionAsync(
        string tenantId,
        PlanCode planCode,
        int trialDays,
        CancellationToken cancellationToken = default);

    Task UpdateManualInvoiceAsync(
        string tenantId,
        ManualInvoiceStatus status,
        string? invoiceReference,
        string? invoiceNotes,
        CancellationToken cancellationToken = default);

    Task ActivateAsync(string tenantId, CancellationToken cancellationToken = default);

    Task SuspendAsync(string tenantId, string? reason, CancellationToken cancellationToken = default);

    Task CancelAsync(string tenantId, string? reason, CancellationToken cancellationToken = default);

    Task ChangePlanAsync(
        string tenantId,
        string planId,
        string? pendingPlanId,
        CancellationToken cancellationToken = default);
}
