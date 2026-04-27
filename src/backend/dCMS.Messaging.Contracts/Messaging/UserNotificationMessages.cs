namespace dCMS.Core.Messaging;

/// <summary>
/// P2 #6: services emit this when they need a row written into the user notification feed.
/// Consumed by dCMS.Notification.Worker, which writes into dcms_notification.NotificationEvents.
/// </summary>
public sealed record UserNotificationCreatedV1(
    string TenantId,
    string UserId,
    string Type,
    string EntityId,
    string Message,
    DateTimeOffset OccurredAt);
