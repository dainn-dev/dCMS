namespace dCMS.Billing.Domain;

public sealed class TenantSubscriptionRecord
{
    public required string TenantId { get; init; }
    public required string PlanId { get; init; }
    public required PlanCode PlanCode { get; init; }
    public required string PlanName { get; init; }
    public required int MaxBrands { get; init; }
    public required int MaxActiveProducts { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public required TenantSubscriptionState SubscriptionState { get; init; }
    public required ManualInvoiceStatus ManualInvoiceStatus { get; init; }
    public required bool TenantActive { get; init; }
    public DateTimeOffset? TrialEndsAt { get; init; }
    public DateTimeOffset? CurrentPeriodStart { get; init; }
    public DateTimeOffset? CurrentPeriodEnd { get; init; }
    public string? PendingPlanId { get; init; }
    public DateTimeOffset? SuspendedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public string? InvoiceReference { get; init; }
    public string? InvoiceNotes { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
