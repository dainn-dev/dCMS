using dCMS.Provisioning.Domain;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Platform;

public sealed class TenantWebhookSubscriptionRepositoryTests
{
    [Fact]
    public void Webhook_subscription_status_round_trips_db_string()
    {
        WebhookSubscriptionStatusExtensions.FromDbString("active").Should().Be(WebhookSubscriptionStatus.Active);
        WebhookSubscriptionStatus.Disabled.ToDbString().Should().Be("disabled");
    }

    [Fact]
    public void Webhook_delivery_status_round_trips_db_string()
    {
        WebhookDeliveryStatusExtensions.FromDbString("dead_letter").Should().Be(WebhookDeliveryStatus.DeadLetter);
        WebhookDeliveryStatus.Delivered.ToDbString().Should().Be("delivered");
    }
}
