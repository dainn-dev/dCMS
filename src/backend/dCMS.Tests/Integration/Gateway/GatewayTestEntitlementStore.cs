using dCMS.Billing.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace dCMS.Tests.Integration.Gateway;

/// <summary>Test double so gateway WAF boots without Redis.</summary>
internal sealed class GatewayTestEntitlementStore : ITenantEntitlementStore
{
    public Task<TenantEntitlementSnapshot?> TryGetAsync(string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<TenantEntitlementSnapshot?>(TenantEntitlementSnapshot.Create(
            tenantId,
            PlanCode.Starter,
            TenantSubscriptionState.Active,
            ManualInvoiceStatus.None,
            tenantActive: true,
            trialEndsAt: null,
            maxBrands: 2,
            maxActiveProducts: 500,
            ["catalog.write"],
            version: 1));

    public Task PublishAsync(TenantEntitlementSnapshot snapshot, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<long> BumpVersionAsync(string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(1L);
}

internal static class GatewayWebApplicationFactoryExtensions
{
    internal static void UseGatewayTestEntitlementStore(this IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(ITenantEntitlementStore)).ToList())
                services.Remove(d);
            services.AddSingleton<ITenantEntitlementStore, GatewayTestEntitlementStore>();
        });
    }
}
