using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>
/// Campaign management persistence (DAI-598).
/// All methods scope queries by TenantId for multi-tenant isolation.
/// </summary>
public interface ICampaignPersistence
{
    Task<(IReadOnlyList<CampaignRow> Items, int Total)> ListCampaignsAsync(
        string  tenantId,
        string? status,
        string? channel,
        string? search,
        int     page,
        int     pageSize,
        CancellationToken cancellationToken = default);

    Task<CampaignRow?> GetCampaignAsync(string id, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> CampaignCodeExistsAsync(string tenantId, string code, string? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>Insert campaign row. Returns the id passed in.</summary>
    Task<string> CreateCampaignAsync(CampaignRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateCampaignAsync(CampaignRow row, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete. Returns false if not found. Caller must validate state allows deletion.</summary>
    Task<bool> DeleteCampaignAsync(string id, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update WorkflowState + append history row in a single transaction.
    /// Returns false if campaign not found or transition not valid per DAG.
    /// </summary>
    Task<bool> TransitionWorkflowAsync(
        string id,
        string tenantId,
        string toState,
        string actorUserId,
        string comment,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignWorkflowHistoryRow>> GetWorkflowHistoryAsync(
        string id, string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DAI-679: load all active campaigns for the tenant whose validity window covers <paramref name="now"/>.
    /// Used by the rule-engine evaluator. Filters: WorkflowState='active' AND
    /// (StartDate IS NULL OR StartDate &lt;= now) AND (EndDate IS NULL OR EndDate &gt;= now).
    /// </summary>
    Task<IReadOnlyList<CampaignRow>> GetActiveByTenantAsync(
        string tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
