using System.Net;
using dCMS.Gateway;
using dCMS.Infrastructure.Routing;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dCMS.Tests.Integration.Gateway;

public sealed class GatewayHostRoutingMiddlewareTests
{
    private static WebApplicationFactory<GatewayAssemblyMarker> FactoryWithResolver(
        IHostTenantResolver resolver,
        bool failClosed = true) =>
        new WebApplicationFactory<GatewayAssemblyMarker>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:Enabled", "false");
            b.UseSetting("HostRouting:Enabled", "true");
            b.UseSetting("HostRouting:FailClosedOnStorefront", failClosed ? "true" : "false");
            b.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            b.UseSetting("ReverseProxy:Clusters:orders-cluster:Destinations:default:Address", "http://127.0.0.1:19999");
            b.UseGatewayTestEntitlementStore();
            b.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IHostTenantResolver)).ToList())
                    services.Remove(d);
                services.AddSingleton(resolver);
            });
        });

    [Fact]
    public async Task Unknown_custom_host_on_storefront_returns_404_when_fail_closed()
    {
        using var factory = FactoryWithResolver(new FixedHostTenantResolver(null));
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/storefront/v1/orders/api/orders");
        request.Headers.Host = "shop.unknown.example";

        var resp = await client.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resolved_host_allows_storefront_request_past_host_routing()
    {
        using var factory = FactoryWithResolver(new FixedHostTenantResolver(new HostTenantResolution("t-shop", "store-1")));
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/storefront/v1/orders/api/orders");
        request.Headers.Host = "shop.acme.example";

        var resp = await client.SendAsync(request);
        resp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Localhost_bypasses_fail_closed()
    {
        using var factory = FactoryWithResolver(new FixedHostTenantResolver(null));
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/storefront/v1/orders/api/orders");
        resp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    private sealed class FixedHostTenantResolver(HostTenantResolution? resolution) : IHostTenantResolver
    {
        public Task<HostTenantResolution?> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
            Task.FromResult(resolution);
    }
}
