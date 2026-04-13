using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace dCMS.Infrastructure.Messaging;

public static class MessageConsumerInfraServiceCollectionExtensions
{
    /// <summary>Registers <see cref="PostgresMessageIdempotencyService"/> against <paramref name="connectionStringName"/>.</summary>
    public static IServiceCollection AddPostgresConsumedMessageIdempotency(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName)
    {
        services.AddSingleton<IIdempotencyService>(sp =>
        {
            var cs = configuration.GetConnectionString(connectionStringName)
                ?? throw new InvalidOperationException(
                    $"ConnectionStrings:{connectionStringName} is required for message idempotency.");
            var logger = sp.GetService<ILogger<PostgresMessageIdempotencyService>>();
            return new PostgresMessageIdempotencyService(cs, logger);
        });
        return services;
    }

    /// <summary>DAI-349 — daily cleanup of <c>ProcessedMessages</c> older than 7 days (02:00 UTC).</summary>
    public static IServiceCollection AddProcessedMessagesCleanup(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName)
    {
        services.AddHostedService(sp =>
            new ProcessedMessagesCleanupHostedService(
                configuration,
                connectionStringName,
                sp.GetRequiredService<ILogger<ProcessedMessagesCleanupHostedService>>()));
        return services;
    }
}
