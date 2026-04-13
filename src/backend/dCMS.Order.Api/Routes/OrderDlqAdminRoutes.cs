using dCMS.AspNetCore.Auth;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Order.Api.Routes;

/// <summary>US-F4 / DAI-362 — SuperAdmin DLQ admin for Order outbox dead letters.</summary>
public static class OrderDlqAdminRoutes
{
    public static void MapOrderDlqAdminRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/orders/dlq")
            .WithTags("admin-dlq")
            .RequireAuthorization(DcmsPolicies.OrderDlqAdmin);

        g.MapGet(
                "",
                async (
                    [FromServices] IOrderDlqAdminRepository repo,
                    [FromQuery] string? eventType,
                    [FromQuery] DateTimeOffset? from,
                    [FromQuery] DateTimeOffset? to,
                    [FromQuery] bool includeDiscarded,
                    CancellationToken ct) =>
                {
                    var rows = await repo.ListAsync(
                        string.IsNullOrWhiteSpace(eventType) ? null : eventType.Trim(),
                        from,
                        to,
                        ct).ConfigureAwait(false);
                    if (!includeDiscarded)
                        rows = rows.Where(r => r.DiscardedAt is null).ToList();

                    return Results.Json(new
                    {
                        data = rows.Select(r => new
                        {
                            id = r.Id,
                            orderId = r.OrderId,
                            eventType = r.EventType,
                            failureReason = r.FailureReason,
                            failedAt = r.FailedAt,
                            retryCount = r.SourceRetryCount,
                            reprocessedAt = r.ReprocessedAt,
                            discardedAt = r.DiscardedAt,
                        }),
                        error = (object?)null,
                    });
                })
            .WithName("ListOrderDlq");

        g.MapPost(
                "{id:long}/retry",
                async ([FromRoute] long id, [FromServices] IOrderDlqAdminRepository repo, CancellationToken ct) =>
                {
                    var ok = await repo.RetryAsync(id, ct).ConfigureAwait(false);
                    if (!ok)
                    {
                        return Results.Json(
                            new { data = (object?)null, error = new { code = "NOT_FOUND_OR_LOCKED", message = "DLQ row missing, already retried, or discarded." } },
                            statusCode: StatusCodes.Status409Conflict);
                    }

                    return Results.Json(new { data = new { id }, error = (object?)null });
                })
            .WithName("RetryOrderDlq");

        g.MapPost(
                "{id:long}/discard",
                async (
                    [FromRoute] long id,
                    [FromBody] DlqDiscardBody? body,
                    [FromServices] IOrderDlqAdminRepository repo,
                    CancellationToken ct) =>
                {
                    var reason = body?.Reason;
                    var ok = await repo.DiscardAsync(id, reason ?? "", ct).ConfigureAwait(false);
                    if (!ok)
                    {
                        return Results.Json(
                            new { data = (object?)null, error = new { code = "NOT_FOUND_OR_DISCARDED", message = "DLQ row missing or already discarded." } },
                            statusCode: StatusCodes.Status409Conflict);
                    }

                    return Results.Json(new { data = new { id }, error = (object?)null });
                })
            .WithName("DiscardOrderDlq");
    }

    public sealed record DlqDiscardBody(string? Reason);
}
