using dCMS.Infrastructure.Platform;
using dCMS.Provisioning.Domain;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Platform;

public sealed class TenantUsageRepositoryTests
{
    [Fact]
    public void TenantUsageCounters_defaults_to_zero()
    {
        var c = new TenantUsageCounters();
        c.OrdersDelta.Should().Be(0);
        c.ApiCallsDelta.Should().Be(0);
        c.WebhookDeliveriesDelta.Should().Be(0);
        c.ActiveProductsDelta.Should().Be(0);
    }
}
