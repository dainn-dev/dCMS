using dCMS.Payment.Infrastructure.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Payment.Infrastructure;

internal sealed class PaymentDbMigrationHostedService(
    IConfiguration configuration,
    ILogger<PaymentDbMigrationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        PaymentDatabaseUpgrader.Run(configuration, logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

