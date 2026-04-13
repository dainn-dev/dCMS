using dCMS.Inventory.Infrastructure.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Inventory.Infrastructure;

public sealed class InventoryDbMigrationHostedService(
    IConfiguration configuration,
    ILogger<InventoryDbMigrationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        InventoryDatabaseUpgrader.Run(configuration, logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
