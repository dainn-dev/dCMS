using dCMS.Provisioning.Domain;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Provisioning;

public sealed class ProvisioningStatusTransitionsTests
{
    [Theory]
    [InlineData(ProvisioningStatus.Requested, ProvisioningStatus.Provisioning, true)]
    [InlineData(ProvisioningStatus.Provisioning, ProvisioningStatus.Active, true)]
    [InlineData(ProvisioningStatus.Provisioning, ProvisioningStatus.Failing, true)]
    [InlineData(ProvisioningStatus.Failing, ProvisioningStatus.Retrying, true)]
    [InlineData(ProvisioningStatus.Failing, ProvisioningStatus.Rollback, true)]
    [InlineData(ProvisioningStatus.Active, ProvisioningStatus.Suspended, true)]
    [InlineData(ProvisioningStatus.Active, ProvisioningStatus.Requested, false)]
    [InlineData(ProvisioningStatus.Deprovisioned, ProvisioningStatus.Active, false)]
    public void CanTransition_matches_state_machine(ProvisioningStatus from, ProvisioningStatus to, bool expected)
    {
        ProvisioningStatusTransitions.CanTransition(from, to).Should().Be(expected);
    }

    [Theory]
    [InlineData(ProvisioningStatus.Failing, "failed")]
    [InlineData(ProvisioningStatus.Active, "active")]
    public void ToApiString_aliases_failing_as_failed(ProvisioningStatus status, string expected)
    {
        status.ToApiString().Should().Be(expected);
    }

    [Fact]
    public void DbString_round_trip_covers_all_statuses()
    {
        foreach (ProvisioningStatus status in Enum.GetValues<ProvisioningStatus>())
        {
            var db = status.ToDbString();
            ProvisioningStatusTransitions.FromDbString(db).Should().Be(status);
        }
    }
}
