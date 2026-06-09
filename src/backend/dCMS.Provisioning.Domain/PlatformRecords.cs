namespace dCMS.Provisioning.Domain;

public sealed record TenantWebhookSubscriptionRecord(
    string Id,
    string TenantId,
    string Url,
    string Secret,
    IReadOnlyList<string> Events,
    WebhookSubscriptionStatus Status,
    int FailureCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public enum WebhookSubscriptionStatus
{
    Active,
    Disabled,
    Failed,
}

public static class WebhookSubscriptionStatusExtensions
{
    public static string ToDbString(this WebhookSubscriptionStatus status) => status switch
    {
        WebhookSubscriptionStatus.Active => "active",
        WebhookSubscriptionStatus.Disabled => "disabled",
        WebhookSubscriptionStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static WebhookSubscriptionStatus FromDbString(string value) => value switch
    {
        "active" => WebhookSubscriptionStatus.Active,
        "disabled" => WebhookSubscriptionStatus.Disabled,
        "failed" => WebhookSubscriptionStatus.Failed,
        _ => WebhookSubscriptionStatus.Disabled,
    };
}

public sealed record TenantWebhookDeliveryRecord(
    long Id,
    string SubscriptionId,
    string TenantId,
    string EventType,
    string PayloadJson,
    string IdempotencyKey,
    WebhookDeliveryStatus Status,
    int AttemptCount,
    int? LastHttpStatus,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt);

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed,
    DeadLetter,
}

public static class WebhookDeliveryStatusExtensions
{
    public static string ToDbString(this WebhookDeliveryStatus status) => status switch
    {
        WebhookDeliveryStatus.Pending => "pending",
        WebhookDeliveryStatus.Delivered => "delivered",
        WebhookDeliveryStatus.Failed => "failed",
        WebhookDeliveryStatus.DeadLetter => "dead_letter",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static WebhookDeliveryStatus FromDbString(string value) => value switch
    {
        "pending" => WebhookDeliveryStatus.Pending,
        "delivered" => WebhookDeliveryStatus.Delivered,
        "failed" => WebhookDeliveryStatus.Failed,
        "dead_letter" => WebhookDeliveryStatus.DeadLetter,
        _ => WebhookDeliveryStatus.Failed,
    };
}

public sealed record IntegrationAppRecord(
    string Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TenantIntegrationRecord(
    string Id,
    string TenantId,
    string AppId,
    string ClientId,
    string ClientSecretHash,
    TenantIntegrationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public enum TenantIntegrationStatus
{
    Active,
    Revoked,
}

public static class TenantIntegrationStatusExtensions
{
    public static string ToDbString(this TenantIntegrationStatus status) => status switch
    {
        TenantIntegrationStatus.Active => "active",
        TenantIntegrationStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static TenantIntegrationStatus FromDbString(string value) => value switch
    {
        "active" => TenantIntegrationStatus.Active,
        "revoked" => TenantIntegrationStatus.Revoked,
        _ => TenantIntegrationStatus.Revoked,
    };
}

public sealed record TenantUsageSnapshot(
    string TenantId,
    DateOnly UsageDate,
    long OrdersCount,
    long ApiCallsCount,
    long WebhookDeliveriesCount,
    long ActiveProductsCount,
    DateTimeOffset UpdatedAt);

public sealed record TenantFeatureOverrideRecord(
    string TenantId,
    string Feature,
    bool Enabled,
    DateTimeOffset UpdatedAt);
