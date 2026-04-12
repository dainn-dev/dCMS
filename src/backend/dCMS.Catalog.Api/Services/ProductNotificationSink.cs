using System.Collections.Generic;
using dCMS.Core.Notifications;
using dCMS.Core.Persistence;
using Microsoft.Extensions.Options;

namespace dCMS.Catalog.Api.Services;

public sealed class ProductNotificationSink(ICatalogPersistence persistence, IOptions<CatalogNotificationOptions> options)
    : IProductNotificationSink
{
    private readonly ICatalogPersistence _persistence = persistence;
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
            await _persistence
                .InsertNotificationAsync(tenantId, uid, "product_submitted", productId, msg, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task OnProductApprovedAsync(string tenantId, string storeId, string productId,
        CancellationToken cancellationToken = default)
    {
        var submitter = await _persistence
            .GetLatestApprovalCommentUserIdAsync(productId, tenantId, storeId, "submitted", cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(submitter))
            return;
        var uid = submitter.Trim();
        if (uid.Length > 64)
            return;
        await _persistence
            .InsertNotificationAsync(tenantId, uid, "product_approved", productId, $"Product {productId} was approved.",
                DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task OnProductRejectedAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default)
    {
        var submitter = await _persistence
            .GetLatestApprovalCommentUserIdAsync(productId, tenantId, storeId, "submitted", cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(submitter))
            return;
        var uid = submitter.Trim();
        if (uid.Length > 64)
            return;
        var preview = message.Length > 500 ? message[..500] + "…" : message;
        await _persistence
            .InsertNotificationAsync(tenantId, uid, "product_rejected", productId, $"Product {productId} was rejected: {preview}",
                DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task OnProductRequestChangesAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default)
    {
        var submitter = await _persistence
            .GetLatestApprovalCommentUserIdAsync(productId, tenantId, storeId, "submitted", cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(submitter))
            return;
        var uid = submitter.Trim();
        if (uid.Length > 64)
            return;
        var preview = message.Length > 500 ? message[..500] + "…" : message;
        await _persistence
            .InsertNotificationAsync(tenantId, uid, "product_request_changes", productId,
                $"Changes requested for product {productId}: {preview}", DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }
}
