using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Services;
using Microsoft.Extensions.Configuration;

namespace dCMS.Catalog.Api.Notifications;

public static class NotificationRoutes
{
    public static void MapNotificationRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/notifications")
            .WithTags("catalog-notifications")
            .WithTenantStoreAccess(configuration);

        Auth(g.MapGet("unread-count", GetUnreadCount), auth, write: false);
        Auth(g.MapGet("", ListNotifications), auth, write: false);
        Auth(g.MapPatch("read-all", MarkAllRead), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder builder, bool authEnabled, bool write) =>
        authEnabled
            ? builder.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : builder;

    private static string ActorUserId(HttpContext http) =>
        http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

    private static async Task<IResult> GetUnreadCount(
        HttpContext http,
        string tenantId,
        string storeId,
        NotificationService notifications,
        CancellationToken cancellationToken)
    {
        var userId = ActorUserId(http);
        var count = await notifications.GetUnreadCountAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { count });
    }

    private static async Task<IResult> ListNotifications(
        HttpContext http,
        string tenantId,
        string storeId,
        int? limit,
        NotificationService notifications,
        CancellationToken cancellationToken)
    {
        var userId = ActorUserId(http);
        var lim = limit ?? 20;
        var list = await notifications.ListForUserAsync(tenantId, userId, lim, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new
        {
            items = list.Select(n => new
            {
                id = n.Id,
                type = n.Type,
                entityId = n.EntityId,
                message = n.Message,
                readAt = n.ReadAt,
                createdAt = n.CreatedAt
            }).ToList()
        });
    }

    private static async Task<IResult> MarkAllRead(
        HttpContext http,
        string tenantId,
        string storeId,
        NotificationService notifications,
        CancellationToken cancellationToken)
    {
        var userId = ActorUserId(http);
        var now = DateTimeOffset.UtcNow;
        var updated = await notifications.MarkAllReadAsync(tenantId, userId, now, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { updated, readAt = now });
    }
}
