using dCMS.Order.Infrastructure.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure;

internal sealed class OrderDbMigrationHostedService(
    IConfiguration configuration,
    ILogger<OrderDbMigrationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        OrderDatabaseUpgrader.Run(configuration, logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
