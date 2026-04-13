using dCMS.Infrastructure.Monitoring;
using Xunit;

namespace dCMS.Tests.Unit.Monitoring;

public sealed class RabbitMqDlqMonitorTests
{
    [Theory]
    [InlineData("dlq.order", true)]
    [InlineData("DLQ.foo", true)]
    [InlineData("dlq", false)]
    [InlineData("my-dlq.foo", false)]
    [InlineData("", false)]
    public void IsDlqQueueName_matches_dlq_dot_prefix(string name, bool expected) =>
        Assert.Equal(expected, RabbitMqDlqDepthMonitorHostedService.IsDlqQueueName(name));
}
