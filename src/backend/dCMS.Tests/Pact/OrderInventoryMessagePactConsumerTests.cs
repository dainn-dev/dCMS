using System.Text.Json;
using dCMS.Core.Messaging;
using PactNet;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-308 — Order consumes Inventory reservation lifecycle messages (contract-only until Order exists).</summary>
public sealed class OrderInventoryMessagePactConsumerTests
{
    private static string PactOutputDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts"));

    [Fact]
    public void OrderService_defines_expected_inventory_messages()
    {
        Directory.CreateDirectory(PactOutputDir);

        var pact = global::PactNet.Pact.V4("OrderService", "Inventory", new PactConfig
        {
            PactDir = PactOutputDir,
            DefaultJsonSettings = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
        });

        var at = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero);
        var reserved = new StockReservedV1("00000000-0000-0000-0000-000000000020", "t1", "s1", at);
        var failed = new StockReservationFailedV1(
            "00000000-0000-0000-0000-000000000020",
            "insufficient_stock",
            "t1",
            "s1",
            at);
        var released = new StockReleasedV1("00000000-0000-0000-0000-000000000020", "t1", "s1", at);

        IMessagePactBuilderV4 msg = pact.WithMessageInteractions();

        msg.ExpectsToReceive("StockReserved.v1 from inventory outbox")
            .Given("order has line items pending allocation")
            .WithJsonContent(reserved)
            .Verify<StockReservedV1>(m =>
            {
                Assert.Equal(reserved.OrderId, m.OrderId);
                Assert.Equal(reserved.TenantId, m.TenantId);
                Assert.Equal(reserved.StoreId, m.StoreId);
                Assert.Equal(reserved.OccurredAt, m.OccurredAt);
            });

        msg.ExpectsToReceive("StockReservationFailed.v1 from inventory outbox")
            .Given("requested quantity unavailable")
            .WithJsonContent(failed)
            .Verify<StockReservationFailedV1>(m =>
            {
                Assert.Equal(failed.OrderId, m.OrderId);
                Assert.Equal(failed.Reason, m.Reason);
                Assert.Equal(failed.TenantId, m.TenantId);
                Assert.Equal(failed.StoreId, m.StoreId);
                Assert.Equal(failed.OccurredAt, m.OccurredAt);
            });

        msg.ExpectsToReceive("StockReleased.v1 from inventory outbox")
            .Given("order cancelled after reservation")
            .WithJsonContent(released)
            .Verify<StockReleasedV1>(m =>
            {
                Assert.Equal(released.OrderId, m.OrderId);
                Assert.Equal(released.TenantId, m.TenantId);
                Assert.Equal(released.StoreId, m.StoreId);
                Assert.Equal(released.OccurredAt, m.OccurredAt);
            });
    }
}
