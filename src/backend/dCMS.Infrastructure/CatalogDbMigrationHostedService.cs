using dCMS.Infrastructure.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure;

public sealed class CatalogDbMigrationHostedService(
    IConfiguration configuration,
    ILogger<CatalogDbMigrationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CatalogDatabaseUpgrader.Run(configuration, logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
