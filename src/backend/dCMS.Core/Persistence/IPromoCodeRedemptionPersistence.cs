using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>
/// DAI-692: redemption tracking. All operations tenant-scoped.
/// Caps must be queried fresh — never cached — since they are authoritative.
/// </summary>
public interface IPromoCodeRedemptionPersistence
{
    /// <summary>Count of confirmed redemptions for (tenant, customer, promoCode).</summary>
    Task<int> GetUsageCountByCustomerAsync(
        string tenantId, string promoCodeId, string customerId,
        CancellationToken cancellationToken = default);

    /// <summary>Count of confirmed redemptions for (tenant, promoCode) — across all customers.</summary>
    Task<int> GetTotalUsageAsync(
        string tenantId, string promoCodeId,
        CancellationToken cancellationToken = default);

    /// <summary>True iff the customer has any confirmed redemption in (tenant, customer, groupId).</summary>
    Task<bool> HasGroupConflictAsync(
        string tenantId, string customerId, string groupId, string excludePromoCodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotent insert (UNIQUE on tenant+promoCode+order). Returns true if a new row was inserted,
    /// false if a redemption already exists for that order (no-op).
    /// </summary>
    Task<bool> InsertConfirmedAsync(
        PromoCodeRedemptionRow row, CancellationToken cancellationToken = default);

    /// <summary>Flip status to 'released' for the order's redemption. Returns rows affected.</summary>
    Task<int> MarkReleasedAsync(
        string tenantId, string orderId,
        CancellationToken cancellationToken = default);

    /// <summary>Look up an existing redemption row by (tenant, order). Used for idempotent confirm read-back.</summary>
    Task<PromoCodeRedemptionRow?> GetByOrderAsync(
        string tenantId, string orderId,
        CancellationToken cancellationToken = default);
}
