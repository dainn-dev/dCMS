using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Routing;

public static class HostTenantResolutionServiceCollectionExtensions
{
    public static IServiceCollection AddDcmsHostTenantResolution(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisCs = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisCs))
        {
            if (services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
            services.AddSingleton<IHostTenantResolver, RedisHostTenantResolver>();
        }
        else
        {
            services.AddSingleton<IHostTenantResolver, NullHostTenantResolver>();
        }

        return services;
    }
}

internal sealed class NullHostTenantResolver : IHostTenantResolver
{
    public Task<HostTenantResolution?> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
        Task.FromResult<HostTenantResolution?>(null);
}
