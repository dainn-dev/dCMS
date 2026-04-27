using System.Text.Json;

namespace dCMS.Core.Approvals;

public interface IApprovalSubject
{
    string EntityType { get; }

    /// <summary>
    /// Validates the action against the entity current state (if needed) and the request scope.
    /// Returning a non-null error means the transition should be rejected as a 400.
    /// </summary>
    Task<string?> ValidateAsync(string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct);

    /// <summary>
    /// Applies side effects to the underlying entity, typically on Approve/Reject/RequestChanges.
    /// Implementations must be idempotent.
    /// </summary>
    Task ApplyAsync(string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, string actedByUserId,
        CancellationToken ct);
}

