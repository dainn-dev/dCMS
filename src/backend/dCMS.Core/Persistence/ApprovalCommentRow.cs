namespace dCMS.Core.Persistence;

/// <summary>Row from <c>ApprovalComments</c> (catalog DB, migration 009).</summary>
public sealed record ApprovalCommentRow(int Id, string UserId, string Role, string Message, string Type, DateTimeOffset CreatedAt);
