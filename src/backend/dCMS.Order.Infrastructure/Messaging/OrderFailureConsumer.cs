using System.Text.Json;
using dCMS.Core.Messaging;
using dCMS.Infrastructure.Messaging;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;

namespace dCMS.Order.Infrastructure.Messaging;

/// <summary>DAI-631: consumes <see cref="OrderFailedV1"/> and upserts into <c>OrderFailures</c>.</summary>
public sealed class OrderFailureConsumer(IOrderFailureRepository repo) : IConsumer<OrderFailedV1>
{
    public async Task Consume(ConsumeContext<OrderFailedV1> context)
    {
        var m = context.Message;
        if (!Guid.TryParse(m.OrderId, out var orderId))
            return;

        var sourceEventId = TryGetHeader(context, DcmsMessageEnvelopeHeaders.MessageId)
            ?? context.MessageId?.ToString();

        var row = new OrderFailureRow
        {
            OrderId = orderId,
            TenantId = m.TenantId,
            StoreId = m.StoreId,
            FailureStatus = m.FailureStatus,
            FailureReason = m.FailureReason,
            FailureErrorCode = m.FailureErrorCode,
            SourceEventId = sourceEventId,
            FailedAt = m.FailedAt,
            RetryCount = 0,
            LastRetryAt = null,
            ResolvedAt = null,
            ResolvedBy = null,
            LogJson = "[]",
        };

        var logEntry = JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.UtcNow,
            sourceEventId,
            status = m.FailureStatus,
            reason = m.FailureReason,
            errorCode = m.FailureErrorCode,
        });

        await repo.UpsertFailureAsync(row, logEntry, context.CancellationToken).ConfigureAwait(false);
    }

    private static string? TryGetHeader<T>(ConsumeContext<T> ctx, string key) where T : class
    {
        if (!ctx.Headers.TryGetHeader(key, out var obj))
            return null;
        return obj?.ToString();
    }
}

