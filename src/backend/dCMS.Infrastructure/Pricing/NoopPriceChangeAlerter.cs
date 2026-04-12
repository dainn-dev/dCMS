using dCMS.Core.Pricing;

namespace dCMS.Infrastructure.Pricing;

public sealed class NoopPriceChangeAlerter : IPriceChangeAlerter
{
    public Task NotifyLargePriceChangeAsync(string tenantId, string storeId, string productId, decimal oldPrice,
        decimal newPrice, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
