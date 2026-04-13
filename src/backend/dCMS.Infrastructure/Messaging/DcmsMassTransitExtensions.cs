using System.Net.Http;
using dCMS.Core.Exceptions;
using MassTransit;

namespace dCMS.Infrastructure.Messaging;

/// <summary>US-F1 / DAI-348 — shared receive endpoint defaults (outbox, retry, idempotency filter).</summary>
public static class DcmsMassTransitExtensions
{
    /// <summary>Registers publish observer that injects dcm-* headers (DAI-304).</summary>
    public static void AddDcmsPublishEnvelopeObserver(this IBusRegistrationConfigurator cfg) =>
        cfg.AddPublishObserver<DcmsPublishEnvelopeObserver>();

    public static void AddDcmsConsumerEndpointDefaults(this IBusRegistrationConfigurator cfg)
    {
        cfg.AddConfigureEndpointsCallback(
            (context, _, endpointConfigurator) =>
            {
                endpointConfigurator.UseInMemoryOutbox(context);
                endpointConfigurator.UseMessageRetry(r =>
                {
                    r.Intervals(1000, 5000, 30000);
                    r.Ignore<MessageValidationException>();
                    r.Ignore<BusinessRuleException>();
                    r.Ignore<ArgumentException>();
                    r.Ignore<ArgumentOutOfRangeException>();
                    r.Ignore<HttpRequestException>(ex =>
                        ex.StatusCode is { } code
                        && (int)code >= 400
                        && (int)code < 500);
                });
                endpointConfigurator.UseConsumeFilter(typeof(DcmsEnvelopeConsumeFilter<>), context);
                endpointConfigurator.UseConsumeFilter(typeof(MessageIdempotencyConsumeFilter<>), context);
            });
    }
}
