namespace dCMS.Infrastructure.Routing;

public sealed record HostTenantResolution(string TenantId, string StoreId);

public interface IHostTenantResolver
{
    Task<HostTenantResolution?> ResolveAsync(string host, CancellationToken cancellationToken = default);
}
