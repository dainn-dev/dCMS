using dCMS.Core.Exceptions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Messaging;

/// <summary>DAI-304 — when publish pipeline set dcm-* headers, require <see cref="DcmsMessageEnvelopeHeaders.TenantId"/>.</summary>
public sealed class DcmsEnvelopeConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ILogger<DcmsEnvelopeConsumeFilter<T>> _logger;

    public DcmsEnvelopeConsumeFilter(ILogger<DcmsEnvelopeConsumeFilter<T>> logger) => _logger = logger;

    public void Probe(ProbeContext context) => context.CreateFilterScope("dcmsEnvelope");

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var hasEnvelopeMarker =
            context.Headers.TryGetHeader(DcmsMessageEnvelopeHeaders.MessageId, out var mid)
            && mid is not null
            && !string.IsNullOrWhiteSpace(mid.ToString());

        if (hasEnvelopeMarker)
        {
            if (!context.Headers.TryGetHeader(DcmsMessageEnvelopeHeaders.TenantId, out var tenantObj)
                || tenantObj is null
                || string.IsNullOrWhiteSpace(tenantObj.ToString()))
            {
                _logger.LogWarning(
                    "Rejecting message {MessageType}: envelope marker without {Header}.",
                    typeof(T).Name,
                    DcmsMessageEnvelopeHeaders.TenantId);
                throw new MessageValidationException(
                    $"{DcmsMessageEnvelopeHeaders.TenantId} header is required when {DcmsMessageEnvelopeHeaders.MessageId} is present.");
            }
        }

        await next.Send(context).ConfigureAwait(false);
    }
}
