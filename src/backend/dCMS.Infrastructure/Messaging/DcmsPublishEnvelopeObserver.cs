using MassTransit;

namespace dCMS.Infrastructure.Messaging;

/// <summary>DAI-304 — sets dcm-* headers on every publish (typed <see cref="PublishContext{T}"/>).</summary>
public sealed class DcmsPublishEnvelopeObserver : IPublishObserver
{
    public Task PrePublish<T>(PublishContext<T> context)
        where T : class
    {
        if (context.Message is null)
            return Task.CompletedTask;

        if (!context.Headers.TryGetHeader(DcmsMessageEnvelopeHeaders.MessageId, out var existingId)
            || existingId is null
            || string.IsNullOrWhiteSpace(existingId.ToString()))
        {
            context.Headers.Set(DcmsMessageEnvelopeHeaders.MessageId, Guid.NewGuid().ToString("D"));
        }

        if (!context.Headers.TryGetHeader(DcmsMessageEnvelopeHeaders.Timestamp, out var existingTs)
            || existingTs is null
            || string.IsNullOrWhiteSpace(existingTs.ToString()))
        {
            context.Headers.Set(DcmsMessageEnvelopeHeaders.Timestamp, DateTimeOffset.UtcNow.ToString("O"));
        }

        var tenant = DcmsMessageEnvelopeHeaders.TryGetMessageTenant(context.Message);
        if (!string.IsNullOrWhiteSpace(tenant))
            context.Headers.Set(DcmsMessageEnvelopeHeaders.TenantId, tenant.Trim());

        var label = DcmsMessageEnvelopeHeaders.GetMessageTypeLabel(context.Message);
        if (!string.IsNullOrWhiteSpace(label))
            context.Headers.Set(DcmsMessageEnvelopeHeaders.MessageType, label);

        return Task.CompletedTask;
    }

    public Task PostPublish<T>(PublishContext<T> context)
        where T : class =>
        Task.CompletedTask;

    public Task PublishFault<T>(PublishContext<T> context, Exception exception)
        where T : class =>
        Task.CompletedTask;
}
