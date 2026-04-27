using dCMS.Core.Messaging;
using dCMS.Infrastructure.Outbox;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Messaging;

/// <summary>Polls order <c>OutboxEvents</c> and publishes integration messages (e.g. <c>OrderPlacedV1</c> for saga start).</summary>
public sealed class OrderOutboxRelayHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly string _connectionString;
    private readonly ILogger<OrderOutboxRelayHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(500);

    public OrderOutboxRelayHostedService(
        IConfiguration configuration,
        IServiceProvider services,
        ILogger<OrderOutboxRelayHostedService> logger)
    {
        _services = services;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required for outbox relay.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var relay = new SqlOutboxRelay(_connectionString);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _services.CreateAsyncScope();
                var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                await relay
                    .ProcessPendingAsync(
                        async (outboxId, m) =>
                        {
                            var mid = OutboxPublishMessageId.FromOutboxRow(outboxId);
                            switch (m)
                            {
                                case OrderPlacedV1 msg:
                                    await publish
                                        .Publish(msg, ctx => ctx.MessageId = mid, stoppingToken)
                                        .ConfigureAwait(false);
                                    return;
                                case OrderShippedV1 msg:
                                    await publish
                                        .Publish(msg, ctx => ctx.MessageId = mid, stoppingToken)
                                        .ConfigureAwait(false);
                                    return;
                                case OrderDeliveredV1 msg:
                                    await publish
                                        .Publish(msg, ctx => ctx.MessageId = mid, stoppingToken)
                                        .ConfigureAwait(false);
                                    return;
                                case ProductRestockedV1 msg:
                                    await publish
                                        .Publish(msg, ctx => ctx.MessageId = mid, stoppingToken)
                                        .ConfigureAwait(false);
                                    return;
                                case ReturnStatusChangedV1 msg:
                                    await publish
                                        .Publish(msg, ctx => ctx.MessageId = mid, stoppingToken)
                                        .ConfigureAwait(false);
                                    return;
                                default:
                                    await publish.Publish(m, stoppingToken).ConfigureAwait(false);
                                    return;
                            }
                        },
                        stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Order outbox relay tick failed.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
