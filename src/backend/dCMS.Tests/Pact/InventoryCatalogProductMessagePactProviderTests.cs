using dCMS.Core.Messaging;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-307 — Provider verification: Catalog message shape satisfies the Inventory pact file.</summary>
[Collection(PactTestCollections.MessageProvider)]
public sealed class InventoryCatalogProductMessagePactProviderTests
{
    [Fact]
    public void Catalog_publishes_ProductUpdated_v1_matching_inventory_pact()
    {
        var sample = new ProductUpdatedV1(
            "00000000-0000-0000-0000-000000000010",
            "t1",
            "s1",
            new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero));

        PactNetMessageProviderSupport.VerifyMessageContract(
            "Catalog",
            "ProductUpdated.v1 from catalog outbox",
            () => sample,
            "Inventory-Catalog.json");
    }
}
