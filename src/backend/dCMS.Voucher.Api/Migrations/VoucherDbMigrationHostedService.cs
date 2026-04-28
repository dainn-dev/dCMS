using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Voucher.Api.Migrations;

public sealed class VoucherDbMigrationHostedService(
    IConfiguration configuration,
    ILogger<VoucherDbMigrationHostedService> logger) : IHostedService
{
    private const long AdvisoryLockId = 820_741_672;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var cs = configuration.GetConnectionString("Voucher");
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("ConnectionStrings:Voucher missing; skipping DbUp.");
            return Task.CompletedTask;
        }

        EnsureDatabase.For.PostgresqlDatabase(cs);

        using var lockConn = new NpgsqlConnection(cs);
        lockConn.Open();
        using (var lockCmd = lockConn.CreateCommand())
        {
            lockCmd.CommandText = $"SELECT pg_advisory_lock({AdvisoryLockId})";
            lockCmd.ExecuteNonQuery();
        }

        try
        {
            var result = DeployChanges.To
                .PostgresqlDatabase(cs)
                .WithScriptsEmbeddedInAssembly(typeof(VoucherDbMigrationHostedService).Assembly,
                    name => name.Contains(".Migrations.", StringComparison.Ordinal)
                            && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .LogToConsole()
                .Build()
                .PerformUpgrade();

            if (!result.Successful)
                throw new InvalidOperationException("Voucher DbUp failed.", result.Error);

            logger.LogInformation("Voucher DbUp finished successfully.");
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
