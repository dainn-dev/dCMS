namespace dCMS.Core.Approvals;

public sealed record ApprovalHistoryEntry(
    string State,
    string By,
    DateTimeOffset At,
    string? Notes);

