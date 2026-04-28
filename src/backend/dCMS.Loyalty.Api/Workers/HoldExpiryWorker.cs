using dCMS.Core.Messaging;
using dCMS.Loyalty.Api.Persistence;
using MassTransit;

namespace dCMS.Loyalty.Api.Workers;

/// <summary>
/// DAI-689: scans LoyaltyHolds for expired 'Held' rows every <c>Loyalty:HoldExpiry:PollIntervalSeconds</c>
/// (default 60s), releases up to <c>BatchSize</c> per tick, publishes <see cref="LoyaltyReleasedV1"/>
/// with reason="hold_expired".
/// </summary>
public sealed class HoldExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<HoldExpiryWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;
    private readonly bool _enabled;

    public HoldExpiryWorker(IServiceProvider sp, IConfiguration cfg, ILogger<HoldExpiryWorker> logger)
    {
        _sp = sp;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(cfg.GetValue("Loyalty:HoldExpiry:PollIntervalSeconds", 60));
        _batchSize = cfg.GetValue("Loyalty:HoldExpiry:BatchSize", 100);
        _enabled = cfg.GetValue("Loyalty:HoldExpiry:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled) { _logger.LogInformation("Loyalty HoldExpiryWorker disabled via config."); return; }
        _logger.LogInformation("Loyalty HoldExpiryWorker starting; interval={Interval}, batch={Batch}", _interval, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Loyalty HoldExpiryWorker tick failed."); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ILoyaltyStore>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var expired = await store.ListExpiredHoldsAsync(DateTimeOffset.UtcNow, _batchSize, ct);
        if (expired.Count == 0) return;

        _logger.LogInformation("Loyalty HoldExpiryWorker: releasing {Count} expired holds.", expired.Count);

        foreach (var row in expired)
        {
            var release = await store.ReleaseAsync(row.TenantId, row.HoldId, "hold_expired", ct);
            if (!release.Success)
            {
                _logger.LogDebug("Loyalty HoldExpiryWorker: release skipped {Hold}: {Code}", row.HoldId, release.ErrorCode);
                continue;
            }

            await publish.Publish(new LoyaltyReleasedV1(
                row.TenantId, row.OrderId, row.HoldId, row.CustomerId, row.Amount,
                Reason: "hold_expired", OccurredAt: DateTimeOffset.UtcNow), ct);
        }
    }
}
