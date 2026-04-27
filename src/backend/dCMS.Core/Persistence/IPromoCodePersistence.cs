using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>
/// Promo code persistence (DAI-659). All methods scope by TenantId.
/// </summary>
public interface IPromoCodePersistence
{
    Task<(IReadOnlyList<PromoCodeRow> Items, int Total)> ListPromoCodesAsync(
        string tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PromoCodeRow?> GetPromoCodeAsync(string id, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> PromoCodeExistsAsync(string tenantId, string code, string? excludeId,
        CancellationToken cancellationToken = default);

    Task CreatePromoCodeAsync(PromoCodeRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdatePromoCodeAsync(PromoCodeRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update workflow state + append history in one transaction.
    /// Returns false if row not found or transition invalid.
    /// </summary>
    Task<bool> TransitionWorkflowAsync(
        string id,
        string tenantId,
        string toState,
        string actorUserId,
        string comment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DAI-692: load binding/cap fields needed by the evaluator's <c>PromoCodeResolver</c>.
    /// Returns null when the code does not exist for the tenant (case-insensitive code match).
    /// </summary>
    Task<PromoCodeBindingRow?> GetForResolutionAsync(
        string tenantId,
        string code,
        CancellationToken cancellationToken = default);
}
