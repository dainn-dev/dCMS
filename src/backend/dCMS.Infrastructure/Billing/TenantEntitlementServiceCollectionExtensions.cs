using dCMS.Billing.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Billing;

public static class TenantEntitlementServiceCollectionExtensions
{
    public static IServiceCollection AddDcmsTenantEntitlements(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddOptions<TenantEntitlementCacheOptions>()
            .Bind(configuration.GetSection(TenantEntitlementCacheOptions.SectionName));

        var redisCs = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisCs))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
        }

        services.AddSingleton<ITenantEntitlementStore, RedisTenantEntitlementStore>();
        services.AddSingleton<IEntitlementGuard, EntitlementGuard>();
        return services;
    }
}
