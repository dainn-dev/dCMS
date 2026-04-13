using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Migrations;

public static class CatalogDatabaseUpgrader
{
    public static void Run(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("Catalog");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Catalog is missing; skipping DbUp.");
            return;
        }

        EnsureDatabase.For.PostgresqlDatabase(cs);

        var result = DeployChanges.To
            .PostgresqlDatabase(cs)
            .WithScriptsEmbeddedInAssembly(typeof(CatalogDatabaseUpgrader).Assembly,
                name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("Catalog database migration failed.", result.Error);

        logger.LogInformation("Catalog DbUp finished successfully.");
    }
}
