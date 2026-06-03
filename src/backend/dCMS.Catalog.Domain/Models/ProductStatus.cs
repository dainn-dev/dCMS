namespace dCMS.Core.Models;

public enum ProductStatus
{
    Draft = 0,
    PendingApproval = 1,
    Active = 2,
    Hidden = 3,
    Archived = 4,
    PendingArchive = 5
}

public static class ProductStatusExtensions
{
    public static string ToPersistedValue(this ProductStatus status) =>
        status switch
        {
            ProductStatus.Draft => "draft",
            ProductStatus.PendingApproval => "pending_approval",
            ProductStatus.Active => "active",
            ProductStatus.Hidden => "hidden",
            ProductStatus.Archived => "archived",
            ProductStatus.PendingArchive => "pending_archive",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    public static ProductStatus ParsePersisted(string value) =>
        value switch
        {
            "draft" => ProductStatus.Draft,
            "pending_approval" => ProductStatus.PendingApproval,
            "active" => ProductStatus.Active,
            "hidden" => ProductStatus.Hidden,
            "archived" => ProductStatus.Archived,
            "pending_archive" => ProductStatus.PendingArchive,
            _ => throw new ArgumentException($"Unknown product status '{value}'.", nameof(value))
        };
}
