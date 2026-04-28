using System.Collections.Generic;
using dCMS.Core.Messaging;
using dCMS.Core.Notifications;
using dCMS.Core.Persistence;
using MassTransit;
using Microsoft.Extensions.Options;

namespace dCMS.Catalog.Api.Services;

/// <summary>
/// P2 #6: publishes <see cref="UserNotificationCreatedV1"/> instead of writing into dcms_catalog directly.
/// Notification.Worker writes the row into dcms_notification.
/// Approval-comment lookups are read-only against the catalog DB (own service, not cross-domain).
/// </summary>
public sealed class ProductNotificationSink(
    ICatalogPersistence persistence,
    IPublishEndpoint publishEndpoint,
    IOptions<CatalogNotificationOptions> options)
    : IProductNotificationSink
{
    private readonly ICatalogPersistence _persistence = persistence;
    private readonly IPublishEndpoint _publish = publishEndpoint;
    private readonly CatalogNotificationOptions _options = options.Value;

    public async Task OnProductSubmittedAsync(string tenantId, string storeId, string productId, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var msg = $"Product {productId} was submitted for approval.";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in _options.SubmittedNotifyUserIds)
        {
            var uid = (raw ?? "").Trim();
            if (uid.Length == 0 || uid.Length > 64 || !seen.Add(uid))
                continue;
            await PublishAsync(tenantId, uid, "product_submitted", productId, msg, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task OnProductApprovedAsync(string tenantId, string storeId, string productId,
        CancellationToken cancellationToken = default)
    {
        var uid = await ResolveSubmitterAsync(tenantId, storeId, productId, cancellationToken).ConfigureAwait(false);
        if (uid is null) return;
        await PublishAsync(tenantId, uid, "product_approved", productId, $"Product {productId} was approved.",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task OnProductRejectedAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default)
    {
        var uid = await ResolveSubmitterAsync(tenantId, storeId, productId, cancellationToken).ConfigureAwait(false);
        if (uid is null) return;
        var preview = message.Length > 500 ? message[..500] + "…" : message;
        await PublishAsync(tenantId, uid, "product_rejected", productId,
            $"Product {productId} was rejected: {preview}", cancellationToken).ConfigureAwait(false);
    }

    public async Task OnProductRequestChangesAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default)
    {
        var uid = await ResolveSubmitterAsync(tenantId, storeId, productId, cancellationToken).ConfigureAwait(false);
        if (uid is null) return;
        var preview = message.Length > 500 ? message[..500] + "…" : message;
        await PublishAsync(tenantId, uid, "product_request_changes", productId,
            $"Changes requested for product {productId}: {preview}", cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ResolveSubmitterAsync(string tenantId, string storeId, string productId, CancellationToken ct)
    {
        var submitter = await _persistence
            .GetLatestApprovalCommentUserIdAsync(productId, tenantId, storeId, "submitted", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(submitter)) return null;
        var uid = submitter.Trim();
        return uid.Length is 0 or > 64 ? null : uid;
    }

    private Task PublishAsync(string tenantId, string userId, string type, string entityId, string message, CancellationToken ct) =>
        _publish.Publish(new UserNotificationCreatedV1(tenantId, userId, type, entityId, message, DateTimeOffset.UtcNow), ct);
}
