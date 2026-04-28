namespace dCMS.Core.Notifications;

/// <summary>Hooks for <c>NotificationEvents</c> (product lifecycle; optional <c>stock_low</c> wiring elsewhere).</summary>
public interface IProductNotificationSink
{
    Task OnProductSubmittedAsync(string tenantId, string storeId, string productId, string actorUserId, CancellationToken cancellationToken = default);

    Task OnProductApprovedAsync(string tenantId, string storeId, string productId, CancellationToken cancellationToken = default);

    Task OnProductRejectedAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default);

    Task OnProductRequestChangesAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default);
}
