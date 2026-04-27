using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Reports.Worker;

public sealed class AnalyticsDbMigrationHostedService(
    IConfiguration configuration,
    ILogger<AnalyticsDbMigrationHostedService> logger) : IHostedService
{
    // Arbitrary fixed key for pg_advisory_lock so only one process migrates at a time.
    private const long AdvisoryLockId = 903_155_019;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var cs = configuration.GetConnectionString("Analytics");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Analytics is missing; skipping analytics DbUp.");
            return Task.CompletedTask;
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
                .WithScriptsEmbeddedInAssembly(typeof(AnalyticsDbMigrationHostedService).Assembly,
                    name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .LogToConsole()
                .Build()
                .PerformUpgrade();

            if (!result.Successful)
                throw new InvalidOperationException("Analytics database migration failed.", result.Error);

            logger.LogInformation("Analytics DbUp finished successfully.");
        }
        finally
        {
            using var unlockCmd = lockConn.CreateCommand();
            unlockCmd.CommandText = $"SELECT pg_advisory_unlock({AdvisoryLockId})";
            unlockCmd.ExecuteNonQuery();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

