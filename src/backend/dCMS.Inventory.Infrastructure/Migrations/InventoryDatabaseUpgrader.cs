using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Inventory.Infrastructure.Migrations;

public static class InventoryDatabaseUpgrader
{
    public static void Run(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("Inventory");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Inventory is missing; skipping DbUp.");
            return;
        }

        EnsureDatabase.For.PostgresqlDatabase(cs);

        var result = DeployChanges.To
            .PostgresqlDatabase(cs)
            .WithScriptsEmbeddedInAssembly(typeof(InventoryDatabaseUpgrader).Assembly,
                name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("Inventory database migration failed.", result.Error);

        logger.LogInformation("Inventory DbUp finished successfully.");
    }
}
