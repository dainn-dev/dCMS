namespace dCMS.Provisioning.Domain;

public interface ITenantWebhookSubscriptionRepository
{
    Task<TenantWebhookSubscriptionRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantWebhookSubscriptionRecord>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantWebhookSubscriptionRecord>> ListActiveByTenantAndEventAsync(
        string tenantId,
        string eventType,
        CancellationToken cancellationToken = default);

    Task CreateAsync(TenantWebhookSubscriptionRecord record, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(string id, WebhookSubscriptionStatus status, int failureCount, CancellationToken cancellationToken = default);
}

public interface ITenantWebhookDeliveryRepository
{
    Task<long> EnqueueAsync(
        string subscriptionId,
        string tenantId,
        string eventType,
        string payloadJson,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TenantWebhookDeliveryRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task UpdateDeliveryResultAsync(
        long id,
        WebhookDeliveryStatus status,
        int attemptCount,
        int? httpStatus,
        string? error,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantWebhookDeliveryRecord>> ListDeadLetterAsync(
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface IIntegrationAppRepository
{
    Task<IReadOnlyList<IntegrationAppRecord>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<IntegrationAppRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertAsync(IntegrationAppRecord record, CancellationToken cancellationToken = default);
}

public interface ITenantIntegrationRepository
{
    Task<IReadOnlyList<TenantIntegrationRecord>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<TenantIntegrationRecord?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task CreateAsync(TenantIntegrationRecord record, CancellationToken cancellationToken = default);

    Task RevokeAsync(string id, CancellationToken cancellationToken = default);
}

public interface ITenantUsageRepository
{
    Task IncrementAsync(
        string tenantId,
        Action<TenantUsageCounters> mutate,
        CancellationToken cancellationToken = default);

    Task<TenantUsageSnapshot> GetTodayAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantUsageSnapshot>> GetRangeAsync(
        string tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public sealed class TenantUsageCounters
{
    public long OrdersDelta { get; set; }
    public long ApiCallsDelta { get; set; }
    public long WebhookDeliveriesDelta { get; set; }
    public long ActiveProductsDelta { get; set; }
}

public interface ITenantFeatureOverrideRepository
{
    Task<IReadOnlyList<TenantFeatureOverrideRecord>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    Task UpsertAsync(string tenantId, string feature, bool enabled, CancellationToken cancellationToken = default);

    Task DeleteAsync(string tenantId, string feature, CancellationToken cancellationToken = default);
}
