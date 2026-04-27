using System.Security.Claims;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Notification.Api.Routes;

/// <summary>
/// P2 #6: in-app user notification feed (formerly hosted by dCMS.Catalog.Api at the same URL shape).
/// Routes are mounted at /api/v1/tenants/{tenantId}/stores/{storeId}/notifications so the existing
/// gateway path /gateway/v1/notifications/tenants/.../stores/.../notifications stays valid.
/// </summary>
public static class NotificationFeedRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapNotificationFeedRoutes(this WebApplication app)
    {
        var auth = app.Configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/notifications")
            .WithTags("notification-feed");

        Auth(g.MapGet("unread-count", GetUnreadCount), auth, write: false);
        Auth(g.MapGet("", List), auth, write: false);
        Auth(g.MapPatch("read-all", MarkAllRead), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead) : b;

    private static IResult Ok(object data) =>
        Results.Json(new { data, meta = (object?)null, error = (object?)null }, JsonCamel);

    private static string ActorUserId(HttpContext http) =>
        http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

    private static async Task<IResult> GetUnreadCount(
        HttpContext http,
        string tenantId,
        string storeId,
        [FromServices] NotificationEventsRepository repo,
        CancellationToken ct)
    {
        var count = await repo.CountUnreadAsync(tenantId, ActorUserId(http), ct).ConfigureAwait(false);
        return Ok(new { count });
    }

    private static async Task<IResult> List(
        HttpContext http,
        string tenantId,
        string storeId,
        int? limit,
        [FromServices] NotificationEventsRepository repo,
        CancellationToken ct)
    {
        var rows = await repo.ListAsync(tenantId, ActorUserId(http), limit ?? 20, ct).ConfigureAwait(false);
        return Ok(new
        {
            items = rows.Select(n => new
            {
                id = n.Id,
                type = n.Type,
                entityId = n.EntityId,
                message = n.Message,
                readAt = n.ReadAt,
                createdAt = n.CreatedAt,
            }).ToList(),
        });
    }

    private static async Task<IResult> MarkAllRead(
        HttpContext http,
        string tenantId,
        string storeId,
        [FromServices] NotificationEventsRepository repo,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await repo.MarkAllReadAsync(tenantId, ActorUserId(http), now, ct).ConfigureAwait(false);
        return Ok(new { updated, readAt = now });
    }
}
