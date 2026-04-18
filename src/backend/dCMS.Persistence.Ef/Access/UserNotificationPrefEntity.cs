namespace dCMS.Persistence.Ef.Access;

/// <summary>Per-user notification opt-in preferences.
/// Table: <c>user_notification_prefs</c>.</summary>
public sealed class UserNotificationPrefEntity
{
    public int Id { get; set; }

    /// <summary>Umbraco user key (string form of Guid).</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Notification channel/type identifier.</summary>
    public string NotificationType { get; set; } = null!;

    public bool Enabled { get; set; }

    /// <summary>Optional JSON array of tenant IDs that trigger this notification, e.g. <c>["t1","t2"]</c>.</summary>
    public string? TenantIds { get; set; }
}
