using dCMS.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace dCMS.Tests.Unit.Infrastructure;

public sealed class TenantPlanRateLimitTests
{
    [Theory]
    [InlineData("acme:starter", 200)]
    [InlineData("acme:pro", 500)]
    [InlineData("acme:enterprise", 1000)]
    public void ResolvePermitLimit_maps_tier_suffix(string key, int expected)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "200",
            ["RateLimiting:WindowSeconds"] = "60"
        }).Build();
        var r = new TenantPlanRateLimit(cfg, redis: null);
        r.ResolvePermitLimit(key).Should().Be(expected);
    }
}
