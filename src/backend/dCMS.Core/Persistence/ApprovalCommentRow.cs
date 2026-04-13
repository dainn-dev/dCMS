namespace dCMS.Core.Persistence;

/// <summary>Row from <c>ApprovalComments</c> (catalog DB, migration 009).</summary>
public sealed class ApprovalCommentRow
{
    public int Id { get; init; }
    public string UserId { get; init; } = null!;
    public string Role { get; init; } = null!;
    public string Message { get; init; } = null!;
    public string Type { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
}
