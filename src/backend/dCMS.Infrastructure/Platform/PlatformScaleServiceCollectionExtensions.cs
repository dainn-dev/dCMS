using dCMS.Provisioning.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Platform;

public static class PlatformScaleServiceCollectionExtensions
{
    public static IServiceCollection AddDcmsPlatformScale(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var catalogCs = configuration.GetConnectionString("Catalog");
        if (string.IsNullOrWhiteSpace(catalogCs))
            throw new InvalidOperationException("Configure ConnectionStrings:Catalog for platform scale services.");

        var redisCs = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisCs) &&
            services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
        }

        services.AddSingleton<ITenantWebhookSubscriptionRepository>(_ =>
            new SqlTenantWebhookSubscriptionRepository(catalogCs));
        services.AddSingleton<ITenantWebhookDeliveryRepository>(_ =>
            new SqlTenantWebhookDeliveryRepository(catalogCs));
        services.AddSingleton<ITenantUsageRepository>(_ => new SqlTenantUsageRepository(catalogCs));
        services.AddSingleton<ITenantFeatureOverrideRepository>(_ =>
            new SqlTenantFeatureOverrideRepository(catalogCs));
        services.AddSingleton<IIntegrationAppRepository>(_ => new SqlIntegrationAppRepository(catalogCs));
        services.AddSingleton<ITenantIntegrationRepository>(_ =>
            new SqlTenantIntegrationRepository(catalogCs));
        services.AddSingleton<IDomainBindingRedisSync>(sp =>
            new DomainBindingRedisSync(sp.GetService<IConnectionMultiplexer>()));
        services.AddSingleton<TenantWebhookDispatcher>();

        return services;
    }
}
