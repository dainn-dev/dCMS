using dCMS.Core.Messaging;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-308 — Inventory provider verifies all Order-facing reservation messages in one pact file.</summary>
[Collection(PactTestCollections.MessageProvider)]
public sealed class OrderInventoryMessagePactProviderTests
{
    [Fact]
    public void Inventory_publishes_messages_matching_order_service_pact()
    {
        var at = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero);
        var reserved = new StockReservedV1("00000000-0000-0000-0000-000000000020", "t1", "s1", at);
        var failed = new StockReservationFailedV1(
            "00000000-0000-0000-0000-000000000020",
            "insufficient_stock",
            "t1",
            "s1",
            at);
        var released = new StockReleasedV1("00000000-0000-0000-0000-000000000020", "t1", "s1", at);

        PactNetMessageProviderSupport.VerifyMessagePactFile("Inventory", "OrderService-Inventory.json", scenarios =>
        {
            scenarios.Add("StockReserved.v1 from inventory outbox", b =>
            {
                b.WithMetadata(new { ContentType = "application/json" });
                b.WithContent(() => reserved, PactNetMessageProviderSupport.JsonCamelCase);
            });
            scenarios.Add("StockReservationFailed.v1 from inventory outbox", b =>
            {
                b.WithMetadata(new { ContentType = "application/json" });
                b.WithContent(() => failed, PactNetMessageProviderSupport.JsonCamelCase);
            });
            scenarios.Add("StockReleased.v1 from inventory outbox", b =>
            {
                b.WithMetadata(new { ContentType = "application/json" });
                b.WithContent(() => released, PactNetMessageProviderSupport.JsonCamelCase);
            });
        });
    }
}
