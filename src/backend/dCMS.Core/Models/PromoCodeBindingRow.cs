namespace dCMS.Core.Models;

/// <summary>
/// DAI-692: extension fields on PromoCode controlling resolution at evaluate-time.
/// Loaded by <c>IPromoCodePersistence.GetForResolutionAsync</c> together with the base row.
/// </summary>
public sealed record PromoCodeBindingRow(
    string Id,
    string TenantId,
    string Code,
    string WorkflowState,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    string? CampaignId,
    string? CustomerId,
    string? GroupId,
    int? MaxUsesPerCustomer,
    int? MaxTotalUses,
    string ExcludedProductsJson);
