namespace dCMS.Core.Notifications;

public sealed class NullProductNotificationSink : IProductNotificationSink
{
    public static readonly NullProductNotificationSink Instance = new();

    public Task OnProductSubmittedAsync(string tenantId, string storeId, string productId, string actorUserId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task OnProductApprovedAsync(string tenantId, string storeId, string productId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnProductRejectedAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task OnProductRequestChangesAsync(string tenantId, string storeId, string productId, string message,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
