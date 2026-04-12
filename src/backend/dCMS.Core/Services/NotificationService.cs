using dCMS.Core.Persistence;

namespace dCMS.Core.Services;

public sealed class NotificationService(ICatalogPersistence persistence)
{
    private readonly ICatalogPersistence _persistence = persistence;

    public Task<int> GetUnreadCountAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        _persistence.CountUnreadNotificationsAsync(tenantId, userId, cancellationToken);

    public Task<IReadOnlyList<NotificationEventRow>> ListForUserAsync(string tenantId, string userId, int limit,
        CancellationToken cancellationToken = default) =>
        _persistence.ListNotificationsForUserAsync(tenantId, userId, Math.Clamp(limit, 1, 100), cancellationToken);

    public Task<int> MarkAllReadAsync(string tenantId, string userId, DateTimeOffset readAt,
        CancellationToken cancellationToken = default) =>
        _persistence.MarkAllNotificationsReadAsync(tenantId, userId, readAt, cancellationToken);
}
