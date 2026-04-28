using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Infrastructure.Migrations;

public static class CatalogDatabaseUpgrader
{
    // Arbitrary fixed key for the pg_advisory_lock so only one process migrates at a time.
    private const long AdvisoryLockId = 820_741_671;

    public static void Run(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("Catalog");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Catalog is missing; skipping DbUp.");
            return;
        }

        EnsureDatabase.For.PostgresqlDatabase(cs);

        using var lockConn = new NpgsqlConnection(cs);
        lockConn.Open();
        using var lockCmd = lockConn.CreateCommand();
        lockCmd.CommandText = $"SELECT pg_advisory_lock({AdvisoryLockId})";
        lockCmd.ExecuteNonQuery();

        try
        {
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
        finally
        {
            using var unlockCmd = lockConn.CreateCommand();
            unlockCmd.CommandText = $"SELECT pg_advisory_unlock({AdvisoryLockId})";
            unlockCmd.ExecuteNonQuery();
        }
    }
}
