using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Order.Core.Integration;

/// <summary>
/// DAI-693: typed client for the Promotions evaluator + redemption side-effect endpoints.
/// </summary>
public interface IPromotionsClient
{
    /// <summary>
    /// Evaluate a cart against active campaigns + promo code. Returns null on transient failure
    /// when the platform is configured fail-open; otherwise the call surfaces an exception.
    /// </summary>
    Task<EvaluateResponse?> EvaluateAsync(
        EvaluateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotent confirm — UNIQUE (tenant, promoCode, order) makes retries safe.</summary>
    Task ConfirmRedemptionAsync(
        string tenantId, ConfirmRedemptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Flip the redemption row to released; no-op if not found.</summary>
    Task ReleaseRedemptionAsync(
        string tenantId, ReleaseRedemptionRequest request, CancellationToken cancellationToken = default);
}
