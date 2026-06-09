using dCMS.Provisioning.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dCMS.Infrastructure.Provisioning;

public static class TenantProvisioningServiceCollectionExtensions
{
    public static IServiceCollection AddDcmsTenantProvisioning(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var catalogCs = configuration.GetConnectionString("Catalog");
        if (string.IsNullOrWhiteSpace(catalogCs))
            throw new InvalidOperationException("Configure ConnectionStrings:Catalog for tenant provisioning.");

        services.AddSingleton<ITenantProvisioningRepository>(_ =>
            new SqlTenantProvisioningRepository(catalogCs));
        return services;
    }
}
