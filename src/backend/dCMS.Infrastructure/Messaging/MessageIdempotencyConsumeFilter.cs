using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Messaging;

/// <summary>US-F1 / DAI-347 — skip duplicate <see cref="ConsumeContext.MessageId"/> after successful processing.</summary>
public sealed class MessageIdempotencyConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<MessageIdempotencyConsumeFilter<T>> _logger;

    public MessageIdempotencyConsumeFilter(IIdempotencyService idempotency, ILogger<MessageIdempotencyConsumeFilter<T>> logger)
    {
        _idempotency = idempotency;
        _logger = logger;
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("messageIdempotency");

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var id = context.MessageId?.ToString();
        if (string.IsNullOrWhiteSpace(id))
        {
            await next.Send(context).ConfigureAwait(false);
            return;
        }

        id = id.Trim();
        await using (await _idempotency.AcquireOrderingLockAsync(id, context.CancellationToken).ConfigureAwait(false))
        {
            if (await _idempotency.IsProcessedAsync(id, context.CancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(
                    "Skipping already-processed message {MessageId} ({MessageType}).",
                    id,
                    typeof(T).Name);
                return;
            }

            await next.Send(context).ConfigureAwait(false);
            await _idempotency.MarkProcessedAsync(id, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
