using System.Text.Json;
using dCMS.Core.Messaging;
using PactNet;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-307 — Consumer-driven message pact: Catalog worker consumes <see cref="StockUpdatedV1"/> published by Inventory.</summary>
public sealed class CatalogInventoryMessagePactConsumerTests
{
    private static string PactOutputDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts"));

    [Fact]
    public void CatalogWorker_defines_expected_StockUpdated_v1_message()
    {
        Directory.CreateDirectory(PactOutputDir);

        var pact = global::PactNet.Pact.V4("CatalogWorker", "Inventory", new PactConfig
        {
            PactDir = PactOutputDir,
            DefaultJsonSettings = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
        });

        var sample = new StockUpdatedV1(
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "t1",
            "s1",
            42,
            3,
            new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero));

        pact.WithMessageInteractions()
            .ExpectsToReceive("StockUpdated.v1 from inventory outbox")
            .Given("variant has stock rows")
            .WithJsonContent(sample)
            .Verify<StockUpdatedV1>(m =>
            {
                Assert.Equal(sample.VariantId, m.VariantId);
                Assert.Equal(sample.WarehouseId, m.WarehouseId);
                Assert.Equal(sample.TenantId, m.TenantId);
                Assert.Equal(sample.StoreId, m.StoreId);
                Assert.Equal(sample.Quantity, m.Quantity);
                Assert.Equal(sample.ReservedQuantity, m.ReservedQuantity);
            });
    }
}
