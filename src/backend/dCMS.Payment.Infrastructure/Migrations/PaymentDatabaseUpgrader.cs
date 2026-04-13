using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Payment.Infrastructure.Migrations;

public static class PaymentDatabaseUpgrader
{
    public static void Run(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("Payment");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Payment is missing; skipping DbUp.");
            return;
        }

        EnsureDatabase.For.PostgresqlDatabase(cs);

        var result = DeployChanges.To
            .PostgresqlDatabase(cs)
            .WithScriptsEmbeddedInAssembly(typeof(PaymentDatabaseUpgrader).Assembly,
                name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("Payment database migration failed.", result.Error);

        logger.LogInformation("Payment DbUp finished successfully.");
    }
}

