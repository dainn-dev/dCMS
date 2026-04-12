using dCMS.Core.Messaging;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-307 — Provider verification: Inventory message shape satisfies the CatalogWorker pact file.</summary>
[Collection(PactTestCollections.MessageProvider)]
public sealed class CatalogInventoryMessagePactProviderTests
{
    [Fact]
    public void Inventory_publishes_StockUpdated_v1_matching_consumer_pact()
    {
        var sample = new StockUpdatedV1(
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "t1",
            "s1",
            42,
            3,
            new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero));

        PactNetMessageProviderSupport.VerifyMessageContract(
            "Inventory",
            "StockUpdated.v1 from inventory outbox",
            () => sample,
            "CatalogWorker-Inventory.json");
    }
}
