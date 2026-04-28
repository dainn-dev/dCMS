namespace dCMS.Core.Pricing;

/// <summary>US-11 follow-up: notify when price change exceeds threshold (e.g. Slack). No-op implementation until wired.</summary>
public interface IPriceChangeAlerter
{
    Task NotifyLargePriceChangeAsync(string tenantId, string storeId, string productId, decimal oldPrice,
        decimal newPrice, CancellationToken cancellationToken = default);
}
