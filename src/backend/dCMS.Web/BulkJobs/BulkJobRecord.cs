namespace dCMS.Web.BulkJobs;

public sealed class BulkJobRecord
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = null!;
    public string? StoreId { get; init; }
    public string JobKind { get; init; } = null!;
    public int RequestedByUserId { get; init; }
    public string? HangfireJobId { get; set; }
    public string Status { get; set; } = null!;
    public int ProgressProcessed { get; set; }
    public int ProgressTotal { get; set; }
    public int ProgressPercent { get; set; }
    public string? InputBlobRef { get; set; }
    public string? OutputBlobRef { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? CancelRequestedAt { get; set; }
}
