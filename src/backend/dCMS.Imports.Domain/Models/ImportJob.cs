namespace dCMS.Core.Models;

// DAI-684 / DAI-706 — bulk import job row (catalog DB "ImportJobs").
public sealed record ImportJob(
    string Id,
    string TenantId,
    string Type,
    string Status,
    string FileKey,
    int? Total,
    int Processed,
    IReadOnlyList<ImportRowError> Errors,
    string? LastProcessedKey,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record ImportRowError(int RowIndex, string Key, string Message);

public static class ImportJobStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string PartiallyCompleted = "PartiallyCompleted";
}

public static class ImportJobTypes
{
    public const string Products = "products";
    public const string ProductImages = "product-images";
    public const string Inventory = "inventory";
    public const string PromoCodes = "promo-codes";

    public static bool IsValid(string? type) =>
        type is Products or ProductImages or Inventory or PromoCodes;
}
