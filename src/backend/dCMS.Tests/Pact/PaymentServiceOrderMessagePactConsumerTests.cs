using System.Text.Json;
using dCMS.Core.Messaging;
using PactNet;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-308 — Payment consumes Order payment outcome messages (contract-only until services exist).</summary>
public sealed class PaymentServiceOrderMessagePactConsumerTests
{
    private static string PactOutputDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts"));

    [Fact]
    public void PaymentService_defines_expected_order_messages()
    {
        Directory.CreateDirectory(PactOutputDir);

        var pact = global::PactNet.Pact.V4("PaymentService", "Order", new PactConfig
        {
            PactDir = PactOutputDir,
            DefaultJsonSettings = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
        });

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

        IMessagePactBuilderV4 msg = pact.WithMessageInteractions();

        msg.ExpectsToReceive("PaymentCompleted.v1 from order outbox")
            .Given("payment provider captured funds")
            .WithJsonContent(completed)
            .Verify<PaymentCompletedV1>(m =>
            {
                Assert.Equal(completed.OrderId, m.OrderId);
                Assert.Equal(completed.PaymentId, m.PaymentId);
                Assert.Equal(completed.TenantId, m.TenantId);
                Assert.Equal(completed.StoreId, m.StoreId);
                Assert.Equal(completed.OccurredAt, m.OccurredAt);
            });

        msg.ExpectsToReceive("PaymentFailed.v1 from order outbox")
            .Given("payment provider rejected charge")
            .WithJsonContent(failed)
            .Verify<PaymentFailedV1>(m =>
            {
                Assert.Equal(failed.OrderId, m.OrderId);
                Assert.Equal(failed.Reason, m.Reason);
                Assert.Equal(failed.TenantId, m.TenantId);
                Assert.Equal(failed.StoreId, m.StoreId);
                Assert.Equal(failed.OccurredAt, m.OccurredAt);
            });
    }
}
