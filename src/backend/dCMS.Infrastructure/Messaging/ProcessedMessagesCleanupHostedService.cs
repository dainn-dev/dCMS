using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Infrastructure.Messaging;

/// <summary>DAI-349 — <c>DELETE FROM ProcessedMessages WHERE ProcessedAt &lt; NOW() - 7 days</c> once per day at 02:00 UTC.</summary>
public sealed class ProcessedMessagesCleanupHostedService : BackgroundService
{
    private readonly string _connectionString;
    private readonly ILogger<ProcessedMessagesCleanupHostedService> _logger;

    public ProcessedMessagesCleanupHostedService(
        IConfiguration configuration,
        string connectionStringName,
        ILogger<ProcessedMessagesCleanupHostedService> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionStringName);
        _connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{connectionStringName} is required for ProcessedMessages cleanup.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRunUtc();
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await RunCleanupOnce(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "ProcessedMessages cleanup failed.");
            }
        }
    }

    public static TimeSpan DelayUntilNextRunUtc()
    {
        var now = DateTime.UtcNow;
        var next = now.Date.AddHours(2);
        if (now >= next)
            next = next.AddDays(1);
        return next - now;
    }

    private async Task RunCleanupOnce(CancellationToken cancellationToken)
    {
        const string sql = """DELETE FROM "ProcessedMessages" WHERE "ProcessedAt" < NOW() - INTERVAL '7 days'""";
        await using var connection = new NpgsqlConnection(_connectionString);
        var deleted = await connection
            .ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        _logger.LogInformation("ProcessedMessages cleanup removed {Deleted} row(s).", deleted);
    }
}
