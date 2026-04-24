namespace dCMS.Core.Persistence;

/// <summary>Row for <c>GET .../products/pending-approvals</c> (DAI-658).</summary>
public sealed record PendingApprovalListRow(
    string Id,
    string Name,
    string BrandName,
    string CategoryPath,
    string? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    string Status);
