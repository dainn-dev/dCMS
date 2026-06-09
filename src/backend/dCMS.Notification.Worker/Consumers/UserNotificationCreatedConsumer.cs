using dCMS.Core.Messaging;
using dCMS.Infrastructure.Messaging;
using dCMS.Infrastructure.Web;
using dCMS.Notification.Api.Routes;
using MassTransit;

namespace dCMS.Notification.Worker.Consumers;

/// <summary>
/// P2 #6: writes user-targeted notifications into dcms_notification.NotificationEvents.
/// </summary>
public sealed class UserNotificationCreatedConsumer(
    NotificationEventsRepository repo,
    IIdempotencyService idempotency,
    ILogger<UserNotificationCreatedConsumer> log) : IConsumer<UserNotificationCreatedV1>
{
    public async Task Consume(ConsumeContext<UserNotificationCreatedV1> context)
    {
        var m = context.Message;
        var messageId = context.MessageId?.ToString()
            ?? $"user-notif:{m.TenantId}:{m.UserId}:{m.Type}:{m.EntityId}:{m.OccurredAt:o}";
        var correlationId = context.CorrelationId?.ToString() ?? context.MessageId?.ToString() ?? "unknown";

        await using var _ = await idempotency.AcquireOrderingLockAsync(messageId, context.CancellationToken).ConfigureAwait(false);
        if (await idempotency.IsProcessedAsync(messageId, context.CancellationToken).ConfigureAwait(false))
            return;

        await repo.InsertAsync(m.TenantId, m.UserId, m.Type, m.EntityId, m.Message, m.OccurredAt, context.CancellationToken)
            .ConfigureAwait(false);

        await idempotency.MarkProcessedAsync(messageId, context.CancellationToken).ConfigureAwait(false);

        DcmsObservabilityMetrics.ObserveWorkerOperation("notification-worker", "user_notification_created", "succeeded", "none");
        log.LogDebug(
            "Worker operation completed service {Service} operation {Operation} status {Status} failure {FailureReason} correlation {CorrelationId} tenant {Tenant} user {User} type {Type}",
            "notification-worker",
            "user_notification_created",
            "succeeded",
            "none",
            correlationId,
            m.TenantId,
            m.UserId,
            m.Type);
    }
}
