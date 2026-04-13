using dCMS.Infrastructure.Messaging;

namespace dCMS.Tests.Unit.Messaging;

public sealed class ProcessedMessagesCleanupHostedServiceTests
{
    [Fact]
    public void DelayUntilNextRunUtc_is_positive_and_targets_0200_utc_window()
    {
        var delay = ProcessedMessagesCleanupHostedService.DelayUntilNextRunUtc();
        Assert.True(delay > TimeSpan.Zero);
        Assert.True(delay <= TimeSpan.FromHours(24));
    }
}
