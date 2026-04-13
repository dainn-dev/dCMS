using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Migrations;

public static class OrderDatabaseUpgrader
{
    public static void Run(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("Order");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Order is missing; skipping DbUp.");
            return;
        }

        EnsureDatabase.For.PostgresqlDatabase(cs);

        var result = DeployChanges.To
            .PostgresqlDatabase(cs)
            .WithScriptsEmbeddedInAssembly(typeof(OrderDatabaseUpgrader).Assembly, name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("Order database migration failed.", result.Error);

        logger.LogInformation("Order DbUp finished successfully.");
    }
}
