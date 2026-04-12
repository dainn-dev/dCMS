using dCMS.Core.Messaging;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-308 — Order provider verifies Payment-facing messages in one pact file.</summary>
[Collection(PactTestCollections.MessageProvider)]
public sealed class PaymentServiceOrderMessagePactProviderTests
{
    [Fact]
    public void Order_publishes_messages_matching_payment_service_pact()
    {
        var at = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero);
        var completed = new PaymentCompletedV1(
            "00000000-0000-0000-0000-000000000030",
            "00000000-0000-0000-0000-000000000031",
            "t1",
            "s1",
            at);
        var failed = new PaymentFailedV1(
            "00000000-0000-0000-0000-000000000030",
            "card_declined",
            "t1",
            "s1",
            at);

        PactNetMessageProviderSupport.VerifyMessagePactFile("Order", "PaymentService-Order.json", scenarios =>
        {
            scenarios.Add("PaymentCompleted.v1 from order outbox", b =>
            {
                b.WithMetadata(new { ContentType = "application/json" });
                b.WithContent(() => completed, PactNetMessageProviderSupport.JsonCamelCase);
            });
            scenarios.Add("PaymentFailed.v1 from order outbox", b =>
            {
                b.WithMetadata(new { ContentType = "application/json" });
                b.WithContent(() => failed, PactNetMessageProviderSupport.JsonCamelCase);
            });
        });
    }
}
