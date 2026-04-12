namespace dCMS.Catalog.Api;

/// <summary>Optional recipients for <c>product_submitted</c> (JWT <c>sub</c> / Umbraco user id strings).</summary>
public sealed class CatalogNotificationOptions
{
    public const string SectionName = "Catalog:Notifications";

    public string[] SubmittedNotifyUserIds { get; set; } = [];
}
